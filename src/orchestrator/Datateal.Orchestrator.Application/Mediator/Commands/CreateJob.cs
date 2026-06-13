using Datateal.Core.Mediator;
using Datateal.Core.Orchestration;
using Datateal.Orchestrator.Application.Validation;
using Datateal.Orchestrator.Core.Entities;
using Datateal.Orchestrator.Core.Enums;
using Datateal.Orchestrator.Core.Repositories;

namespace Datateal.Orchestrator.Application.Mediator.Commands;

public record CreateJobRequest(
    string Name,
    string? Description,
    Guid? FolderId,
    int MaxConcurrentRuns,
    List<CreateJobTaskRequest>? Tasks = null,
    List<CreateJobParameterRequest>? Parameters = null) : IRequest<Job>;

public record CreateJobTaskRequest(
    string Name,
    TaskType TaskType,
    int MaxRetries,
    TimeSpan RetryInterval,
    TimeSpan? Timeout,
    Guid? NotebookId,
    Guid? QueryId,
    Guid? SubJobId,
    string? NodePoolRef,
    Dictionary<string, string>? Parameters,
    List<CreateTaskDependencyRequest> Dependencies);

public record CreateTaskDependencyRequest(string DependsOnTaskName, DependencyCondition Condition);

public record CreateJobParameterRequest(string Name, string? DefaultValue, bool IsRequired, string? Description);

internal class CreateJobHandler(IJobRepository jobRepository, Datateal.Orchestrator.Core.IWorkspaceContext workspace) : IRequestHandler<CreateJobRequest, Job>
{
    public async Task<Job> Handle(CreateJobRequest request, CancellationToken cancellationToken)
    {
        // Validate unique task names in the submitted task list.
        var taskNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in request.Tasks ?? [])
        {
            if (!taskNames.Add(t.Name))
                throw new InvalidOperationException($"Duplicate task name: \"{t.Name}\". Task names must be unique within a job.");
        }

        // Validate unique job name within the workspace.
        var existing = await jobRepository.GetJobByNameAsync(request.Name, workspace.RequireWorkspaceId(), cancellationToken);
        if (existing is not null)
            throw new JobNameConflictException(request.Name);

        var job = new Job
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspace.RequireWorkspaceId(),
            Name = request.Name,
            Description = request.Description,
            FolderId = request.FolderId,
            MaxConcurrentRuns = request.MaxConcurrentRuns,
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
                    NotebookId = t.NotebookId ?? throw new InvalidOperationException("NotebookId is required for notebook tasks."),
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
                    QueryId = t.QueryId ?? throw new InvalidOperationException("QueryId is required for SQL query tasks."),
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
                    SubJobId = t.SubJobId ?? throw new InvalidOperationException("SubJobId is required for sub-job tasks."),
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

        return await jobRepository.CreateJobAsync(job, cancellationToken);
    }
}
