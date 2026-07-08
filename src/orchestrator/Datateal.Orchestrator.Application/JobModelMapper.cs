using System.Text.Json;
using Datateal.Core.Orchestration;
using Datateal.Deployment.Diff;
using Datateal.Deployment.Models;
using Datateal.Orchestrator.Application.Mediator.Commands;
using Datateal.Orchestrator.Core.Entities;
using Datateal.Orchestrator.Core.Enums;
using Datateal.Orchestrator.Core.Interfaces;
using Datateal.Orchestrator.Core.Repositories;

namespace Datateal.Orchestrator.Application;

internal sealed class JobModelMapper(
    IWorkspaceReader workspaceReader,
    IJobRepository jobRepository) : IResourceMapper<JobModel>
{
    public string ResourceType => "job";

    public string NaturalKey(JobModel model) => model.Name.Trim();

    public async Task<JobModel> ToModelAsync(Job job, CancellationToken ct)
    {
        var taskNamesById = job.Tasks.ToDictionary(task => task.Id, task => task.Name);
        var model = new JobModel
        {
            Name = job.Name,
            Description = job.Description,
            MaxConcurrentRuns = job.MaxConcurrentRuns,
            IsEnabled = job.IsEnabled,
            Parameters = job.Parameters
                .OrderBy(parameter => parameter.Name, StringComparer.OrdinalIgnoreCase)
                .Select(parameter => new JobParameterModel
                {
                    Name = parameter.Name,
                    DefaultValue = parameter.DefaultValue,
                    Required = parameter.IsRequired,
                    Description = parameter.Description,
                })
                .ToList(),
            NodePools = job.Tasks
                .Select(task => task switch
                {
                    NotebookTask notebook => notebook.NodePoolRef,
                    SqlQueryTask query => query.NodePoolRef,
                    _ => null,
                })
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .Select(name => new JobNodePoolModel { Name = name! })
                .ToList(),
            Tasks = [],
            Schedules = job.Schedules
                .OrderBy(schedule => schedule.Name, StringComparer.OrdinalIgnoreCase)
                .Select(schedule => new JobScheduleModel
                {
                    Name = schedule.Name,
                    Cron = schedule.CronExpression,
                    TimeZone = schedule.TimeZone,
                    Parameters = NormalizeDictionary(schedule.Parameters),
                })
                .ToList(),
        };

        foreach (var task in job.Tasks.OrderBy(task => task.Name, StringComparer.OrdinalIgnoreCase))
        {
            var taskModel = new JobTaskModel
            {
                Name = task.Name,
                MaxRetries = task.MaxRetries,
                RetryInterval = task.RetryInterval.ToString("c"),
                Timeout = task.Timeout?.ToString("c"),
                Parameters = task switch
                {
                    NotebookTask notebook => NormalizeDictionary(notebook.Parameters),
                    SqlQueryTask query => NormalizeDictionary(query.Parameters),
                    SubJobTask subJob => NormalizeDictionary(subJob.Parameters),
                    _ => null,
                },
                Dependencies = task.Dependencies
                    .OrderBy(dependency => taskNamesById[dependency.DependsOnTaskId], StringComparer.OrdinalIgnoreCase)
                    .ThenBy(dependency => dependency.Condition)
                    .Select(dependency => new JobTaskDependencyModel
                    {
                        Task = taskNamesById[dependency.DependsOnTaskId],
                        Condition = FormatCondition(dependency.Condition),
                    })
                    .ToList(),
            };

            switch (task)
            {
                case NotebookTask notebook:
                    taskModel.Type = "notebook";
                    taskModel.NotebookPath = TrimSlashes(await workspaceReader.ResolveNotebookPathByIdAsync(notebook.NotebookId, ct));
                    taskModel.NodePoolRef = notebook.NodePoolRef;
                    break;
                case SqlQueryTask query:
                    taskModel.Type = "sql_query";
                    taskModel.QueryPath = TrimSlashes(await workspaceReader.ResolveQueryPathByIdAsync(query.QueryId, ct));
                    taskModel.NodePoolRef = query.NodePoolRef;
                    break;
                case SubJobTask subJob:
                    taskModel.Type = "sub_job";
                    taskModel.JobName = (await jobRepository.GetJobAsync(subJob.SubJobId, ct))?.Name;
                    break;
            }

            model.Tasks.Add(taskModel);
        }

        return model;
    }

    public async Task<CreateJobRequest> ToCreateRequestAsync(
        Guid workspaceId,
        Guid ownerUserId,
        JobModel model,
        CancellationToken ct)
    {
        var resolved = await ResolveTasksAsync(workspaceId, model.Tasks, ct);
        return new CreateJobRequest(
            WorkspaceId: workspaceId,
            Name: NaturalKey(model),
            Description: model.Description,
            FolderId: null,
            MaxConcurrentRuns: model.MaxConcurrentRuns,
            IsEnabled: model.IsEnabled,
            OwnerUserId: ownerUserId,
            Tasks: resolved.Tasks,
            Parameters: model.Parameters
                .OrderBy(parameter => parameter.Name, StringComparer.OrdinalIgnoreCase)
                .Select(parameter => new CreateJobParameterRequest(
                    parameter.Name,
                    parameter.DefaultValue,
                    parameter.Required,
                    parameter.Description))
                .ToList(),
            Schedules: model.Schedules
                .OrderBy(schedule => schedule.Name, StringComparer.OrdinalIgnoreCase)
                .Select(schedule => new CreateJobScheduleRequest(
                    schedule.Name,
                    schedule.Cron,
                    true,
                    schedule.TimeZone,
                    NormalizeDictionary(schedule.Parameters)))
                .ToList());
    }

    public async Task<UpdateJobRequest> ToUpdateRequestAsync(
        Guid workspaceId,
        Guid jobId,
        Guid ownerUserId,
        JobModel model,
        CancellationToken ct)
    {
        var resolved = await ResolveTasksAsync(workspaceId, model.Tasks, ct);
        return new UpdateJobRequest(
            WorkspaceId: workspaceId,
            Id: jobId,
            Name: NaturalKey(model),
            Description: model.Description,
            FolderId: null,
            MaxConcurrentRuns: model.MaxConcurrentRuns,
            IsEnabled: model.IsEnabled,
            OwnerUserId: ownerUserId,
            Tasks: resolved.Updates,
            Parameters: model.Parameters
                .OrderBy(parameter => parameter.Name, StringComparer.OrdinalIgnoreCase)
                .Select(parameter => new UpdateJobParameterRequest(
                    parameter.Name,
                    parameter.DefaultValue,
                    parameter.Required,
                    parameter.Description))
                .ToList(),
            Schedules: model.Schedules
                .OrderBy(schedule => schedule.Name, StringComparer.OrdinalIgnoreCase)
                .Select(schedule => new UpdateJobScheduleRequest(
                    schedule.Name,
                    schedule.Cron,
                    true,
                    schedule.TimeZone,
                    NormalizeDictionary(schedule.Parameters)))
                .ToList());
    }

    public bool AreEqual(JobModel desired, JobModel current) =>
        string.Equals(SerializeCanonical(desired), SerializeCanonical(current), StringComparison.Ordinal);

    private async Task<(List<CreateJobTaskRequest> Tasks, List<UpdateJobTaskRequest> Updates)> ResolveTasksAsync(
        Guid workspaceId,
        IEnumerable<JobTaskModel> tasks,
        CancellationToken ct)
    {
        var models = tasks.OrderBy(task => task.Name, StringComparer.OrdinalIgnoreCase).ToList();
        var createTasks = new List<CreateJobTaskRequest>();
        var updateTasks = new List<UpdateJobTaskRequest>();

        foreach (var task in models)
        {
            var taskType = ParseTaskType(task.Type);
            var retryInterval = ParseNullableTimeSpan(task.RetryInterval) ?? TimeSpan.FromSeconds(30);
            var timeout = ParseNullableTimeSpan(task.Timeout);
            var parameters = NormalizeDictionary(task.Parameters);
            var createDependencies = task.Dependencies
                .OrderBy(dependency => dependency.Task, StringComparer.OrdinalIgnoreCase)
                .Select(dependency => new CreateTaskDependencyRequest(
                    dependency.Task,
                    ParseCondition(dependency.Condition)))
                .ToList();
            var updateDependencies = task.Dependencies
                .OrderBy(dependency => dependency.Task, StringComparer.OrdinalIgnoreCase)
                .Select(dependency => new UpdateJobDependencyRequest(
                    dependency.Task,
                    ParseCondition(dependency.Condition)))
                .ToList();

            Guid? notebookId = null;
            Guid? queryId = null;
            Guid? subJobId = null;

            switch (taskType)
            {
                case TaskType.Notebook:
                    notebookId = await workspaceReader.ResolveNotebookIdByPathAsync(workspaceId, TrimSlashes(task.NotebookPath) ?? string.Empty, ct)
                        ?? throw new InvalidOperationException($"Notebook '{task.NotebookPath}' was not found.");
                    break;
                case TaskType.SqlQuery:
                    queryId = await workspaceReader.ResolveQueryIdByPathAsync(workspaceId, TrimSlashes(task.QueryPath) ?? string.Empty, ct)
                        ?? throw new InvalidOperationException($"Query '{task.QueryPath}' was not found.");
                    break;
                case TaskType.SubJob:
                    subJobId = (await jobRepository.GetJobByNameAsync(task.JobName ?? string.Empty, workspaceId, ct))?.Id
                        ?? throw new InvalidOperationException($"Sub-job '{task.JobName}' was not found.");
                    break;
            }

            createTasks.Add(new CreateJobTaskRequest(
                task.Name,
                taskType,
                task.MaxRetries,
                retryInterval,
                timeout,
                notebookId,
                queryId,
                subJobId,
                task.NodePoolRef,
                parameters,
                createDependencies));

            updateTasks.Add(new UpdateJobTaskRequest(
                task.Name,
                taskType,
                task.MaxRetries,
                retryInterval,
                timeout,
                notebookId,
                queryId,
                subJobId,
                task.NodePoolRef,
                parameters,
                updateDependencies));
        }

        return (createTasks, updateTasks);
    }

    private static string SerializeCanonical(JobModel model) =>
        JsonSerializer.Serialize(Normalize(model));

    private static object Normalize(JobModel model) => new
    {
        Name = model.Name.Trim(),
        model.Description,
        model.MaxConcurrentRuns,
        model.IsEnabled,
        Parameters = model.Parameters
            .OrderBy(parameter => parameter.Name, StringComparer.OrdinalIgnoreCase)
            .Select(parameter => new
            {
                Name = parameter.Name.Trim(),
                parameter.DefaultValue,
                parameter.Required,
                parameter.Description,
            }),
        Tasks = model.Tasks
            .OrderBy(task => task.Name, StringComparer.OrdinalIgnoreCase)
            .Select(task => new
            {
                Name = task.Name.Trim(),
                Type = NormalizeTaskType(task.Type),
                NotebookPath = TrimSlashes(task.NotebookPath),
                QueryPath = TrimSlashes(task.QueryPath),
                task.JobName,
                task.NodePoolRef,
                task.MaxRetries,
                RetryInterval = ParseNullableTimeSpan(task.RetryInterval)?.ToString("c"),
                Timeout = ParseNullableTimeSpan(task.Timeout)?.ToString("c"),
                Parameters = NormalizeDictionary(task.Parameters),
                Dependencies = task.Dependencies
                    .OrderBy(dependency => dependency.Task, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(dependency => NormalizeCondition(dependency.Condition), StringComparer.OrdinalIgnoreCase)
                    .Select(dependency => new
                    {
                        Task = dependency.Task.Trim(),
                        Condition = NormalizeCondition(dependency.Condition),
                    }),
            }),
        Schedules = model.Schedules
            .OrderBy(schedule => schedule.Name, StringComparer.OrdinalIgnoreCase)
            .Select(schedule => new
            {
                Name = schedule.Name.Trim(),
                schedule.Cron,
                schedule.TimeZone,
                Parameters = NormalizeDictionary(schedule.Parameters),
            }),
    };

    private static Dictionary<string, string>? NormalizeDictionary(Dictionary<string, string>? values) =>
        values?
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);

    private static string NormalizeTaskType(string type) =>
        type.Trim().ToLowerInvariant() switch
        {
            "sql" => "sql_query",
            _ => type.Trim().ToLowerInvariant(),
        };

    private static string NormalizeCondition(string condition) =>
        condition.Trim().ToLowerInvariant() switch
        {
            "success" => "on_success",
            "failure" => "on_failure",
            "completion" => "on_completion",
            "skip" => "on_skip",
            _ => condition.Trim().ToLowerInvariant(),
        };

    private static TaskType ParseTaskType(string type) => NormalizeTaskType(type) switch
    {
        "notebook" => TaskType.Notebook,
        "sql_query" => TaskType.SqlQuery,
        "sub_job" => TaskType.SubJob,
        _ => throw new InvalidOperationException($"Unknown task type '{type}'."),
    };

    private static DependencyCondition ParseCondition(string condition) => NormalizeCondition(condition) switch
    {
        "on_success" => DependencyCondition.OnSuccess,
        "on_failure" => DependencyCondition.OnFailure,
        "on_completion" => DependencyCondition.OnCompletion,
        "on_skip" => DependencyCondition.OnSkip,
        _ => throw new InvalidOperationException($"Unknown dependency condition '{condition}'."),
    };

    private static string FormatCondition(DependencyCondition condition) => condition switch
    {
        DependencyCondition.OnSuccess => "on_success",
        DependencyCondition.OnFailure => "on_failure",
        DependencyCondition.OnCompletion => "on_completion",
        DependencyCondition.OnSkip => "on_skip",
        _ => "on_success",
    };

    private static TimeSpan? ParseNullableTimeSpan(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return TimeSpan.TryParse(value, out var parsed)
            ? parsed
            : throw new InvalidOperationException($"Invalid duration '{value}'.");
    }

    private static string? TrimSlashes(string? path) =>
        string.IsNullOrWhiteSpace(path) ? path : path.Trim('/');
}
