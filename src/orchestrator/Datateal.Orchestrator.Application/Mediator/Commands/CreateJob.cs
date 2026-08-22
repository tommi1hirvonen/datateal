using Datateal.Core.Mediator;
using Datateal.Core.Orchestration;
using Datateal.Orchestrator.Application.Engine;
using Datateal.Orchestrator.Application.Validation;
using Datateal.Orchestrator.Core.Entities;
using Datateal.Orchestrator.Core.Enums;
using Datateal.Orchestrator.Core.Interfaces;
using Datateal.Orchestrator.Core.Repositories;

namespace Datateal.Orchestrator.Application.Mediator.Commands;

public record CreateJobRequest(
    Guid WorkspaceId,
    string Name,
    string? Description,
    Guid? FolderId,
    int MaxConcurrentRuns,
    bool IsEnabled = true,
    Guid? OwnerUserId = null,
    List<CreateJobTaskRequest>? Tasks = null,
    List<CreateJobParameterRequest>? Parameters = null,
    List<CreateJobScheduleRequest>? Schedules = null) : IRequest<Job>;

public record CreateJobTaskRequest(
    string Name,
    TaskType TaskType,
    int MaxRetries,
    TimeSpan RetryInterval,
    TimeSpan? Timeout,
    string? NotebookPath,
    string? QueryPath,
    string? SubJobName,
    string? NodePoolRef,
    Dictionary<string, string>? Parameters,
    List<CreateTaskDependencyRequest> Dependencies);

public record CreateTaskDependencyRequest(string DependsOnTaskName, DependencyCondition Condition);

public record CreateJobParameterRequest(string Name, string? DefaultValue, bool IsRequired, string? Description);

public record CreateJobScheduleRequest(string Name, string CronExpression, bool IsEnabled, string? TimeZone, Dictionary<string, string>? Parameters);

internal class CreateJobHandler(
    IJobRepository jobRepository,
    IWorkspaceReader workspaceReader,
    SchedulesManager schedulesManager) : IRequestHandler<CreateJobRequest, Job>
{
    public async Task<Job> Handle(CreateJobRequest request, CancellationToken cancellationToken)
    {
        if (request.OwnerUserId is null)
            throw new InvalidOperationException(JobOwner.MissingOwnerMessage);

        // Validate unique task names in the submitted task list.
        var taskNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in request.Tasks ?? [])
        {
            if (!taskNames.Add(t.Name))
                throw new InvalidOperationException($"Duplicate task name: \"{t.Name}\". Task names must be unique within a job.");
        }

        // Validate unique job name within the workspace.
        var existing = await jobRepository.GetJobByNameAsync(request.Name, request.WorkspaceId, cancellationToken);
        if (existing is not null)
            throw new JobNameConflictException(request.Name);

        var job = new Job
        {
            Id = Guid.NewGuid(),
            WorkspaceId = request.WorkspaceId,
            Name = request.Name,
            Description = request.Description,
            FolderId = request.FolderId,
            MaxConcurrentRuns = request.MaxConcurrentRuns,
            IsEnabled = request.IsEnabled,
            OwnerUserId = request.OwnerUserId,
            CreatedByUserId = request.OwnerUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        foreach (var p in request.Parameters ?? [])
        {
            ParameterNameValidator.Validate(p.Name);
            job.Parameters.Add(new JobParameter
            {
                Id = Guid.NewGuid(),
                JobId = job.Id,
                Name = p.Name,
                DefaultValue = p.DefaultValue,
                IsRequired = p.IsRequired,
                Description = p.Description,
            });
        }

        foreach (var schedule in request.Schedules ?? [])
        {
            job.Schedules.Add(new JobSchedule
            {
                Id = Guid.NewGuid(),
                JobId = job.Id,
                Name = schedule.Name,
                CronExpression = schedule.CronExpression,
                IsEnabled = schedule.IsEnabled,
                TimeZone = schedule.TimeZone,
                Parameters = schedule.Parameters,
            });
        }

        // Build a name→task map so we can resolve dependencies by name
        var tasksByName = new Dictionary<string, JobTask>(StringComparer.OrdinalIgnoreCase);

        foreach (var t in request.Tasks ?? [])
        {
            JobTask task = t.TaskType switch
            {
                TaskType.Notebook => new NotebookTask
                {
                    Id = Guid.NewGuid(),
                    JobId = job.Id,
                    Name = t.Name,
                    MaxRetries = t.MaxRetries,
                    RetryInterval = t.RetryInterval,
                    Timeout = t.Timeout,
                    NotebookPath = string.IsNullOrWhiteSpace(t.NotebookPath)
                        ? throw new InvalidOperationException("NotebookPath is required for notebook tasks.")
                        : await ResolveNotebookPathAsync(request.WorkspaceId, t.NotebookPath, cancellationToken),
                    NodePoolRef = t.NodePoolRef ?? throw new InvalidOperationException("NodePoolRef is required for notebook tasks."),
                    Parameters = t.Parameters,
                },
                TaskType.SqlQuery => new SqlQueryTask
                {
                    Id = Guid.NewGuid(),
                    JobId = job.Id,
                    Name = t.Name,
                    MaxRetries = t.MaxRetries,
                    RetryInterval = t.RetryInterval,
                    Timeout = t.Timeout,
                    QueryPath = string.IsNullOrWhiteSpace(t.QueryPath)
                        ? throw new InvalidOperationException("QueryPath is required for SQL query tasks.")
                        : await ResolveQueryPathAsync(request.WorkspaceId, t.QueryPath, cancellationToken),
                    NodePoolRef = t.NodePoolRef ?? throw new InvalidOperationException("NodePoolRef is required for SQL query tasks."),
                    Parameters = t.Parameters,
                },
                TaskType.SubJob => new SubJobTask
                {
                    Id = Guid.NewGuid(),
                    JobId = job.Id,
                    Name = t.Name,
                    MaxRetries = t.MaxRetries,
                    RetryInterval = t.RetryInterval,
                    Timeout = t.Timeout,
                    SubJobName = string.IsNullOrWhiteSpace(t.SubJobName)
                        ? throw new InvalidOperationException("SubJobName is required for sub-job tasks.")
                        : await ResolveSubJobNameAsync(request.WorkspaceId, t.SubJobName, cancellationToken),
                    Parameters = t.Parameters,
                },
                _ => throw new InvalidOperationException($"Unknown task type: {t.TaskType}")
            };

            tasksByName[t.Name] = task;
            job.Tasks.Add(task);
        }

        // Resolve dependencies by task name
        var tasksList = request.Tasks ?? [];
        for (var i = 0; i < tasksList.Count; i++)
        {
            var taskReq = tasksList[i];
            var task = job.Tasks[i];

            foreach (var dep in taskReq.Dependencies)
            {
                if (!tasksByName.TryGetValue(dep.DependsOnTaskName, out var dependsOnTask))
                    throw new InvalidOperationException($"Task '{task.Name}' depends on unknown task '{dep.DependsOnTaskName}'.");

                task.Dependencies.Add(new TaskDependency
                {
                    Id = Guid.NewGuid(),
                    TaskId = task.Id,
                    DependsOnTaskId = dependsOnTask.Id,
                    Condition = dep.Condition,
                });
            }
        }

        DagValidator.Validate(job.Tasks);

        var created = await jobRepository.CreateJobAsync(job, cancellationToken);
        if (request.Schedules is not null)
        {
            foreach (var schedule in created.Schedules)
                await schedulesManager.AddScheduleAsync(schedule, cancellationToken);
        }

        return created;
    }

    private async Task<string> ResolveNotebookPathAsync(Guid workspaceId, string path, CancellationToken ct)
    {
        _ = await workspaceReader.ResolveNotebookIdByPathAsync(workspaceId, path, ct)
            ?? throw new InvalidOperationException($"Notebook '{path}' was not found in this workspace.");
        return path;
    }

    private async Task<string> ResolveQueryPathAsync(Guid workspaceId, string path, CancellationToken ct)
    {
        _ = await workspaceReader.ResolveQueryIdByPathAsync(workspaceId, path, ct)
            ?? throw new InvalidOperationException($"Query '{path}' was not found in this workspace.");
        return path;
    }

    private async Task<string> ResolveSubJobNameAsync(Guid workspaceId, string name, CancellationToken ct)
    {
        _ = await jobRepository.GetJobByNameAsync(name, workspaceId, ct)
            ?? throw new InvalidOperationException($"Sub-job '{name}' was not found in this workspace.");
        return name;
    }
}
