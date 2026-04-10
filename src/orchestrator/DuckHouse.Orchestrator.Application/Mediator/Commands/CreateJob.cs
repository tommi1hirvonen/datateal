using DuckHouse.Core.Mediator;
using DuckHouse.Orchestrator.Application.Validation;
using DuckHouse.Orchestrator.Core.Entities;
using DuckHouse.Orchestrator.Core.Enums;
using DuckHouse.Orchestrator.Core.Repositories;

namespace DuckHouse.Orchestrator.Application.Mediator.Commands;

public record CreateJobRequest(
    string Name,
    string? Description,
    Guid? FolderId,
    int MaxConcurrentRuns,
    List<CreateJobTaskRequest> Tasks,
    List<CreateJobParameterRequest> Parameters) : IRequest<Job>;

public record CreateJobTaskRequest(
    string Name,
    string TaskType,
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

internal class CreateJobHandler(IJobRepository jobRepository) : IRequestHandler<CreateJobRequest, Job>
{
    public async Task<Job> Handle(CreateJobRequest request, CancellationToken cancellationToken)
    {
        var job = new Job
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            FolderId = request.FolderId,
            MaxConcurrentRuns = request.MaxConcurrentRuns,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        foreach (var p in request.Parameters)
        {
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

        foreach (var t in request.Tasks)
        {
            JobTask task = t.TaskType.ToLowerInvariant() switch
            {
                "notebook" => new NotebookTask
                {
                    Id = Guid.NewGuid(),
                    JobId = job.Id,
                    Name = t.Name,
                    MaxRetries = t.MaxRetries,
                    RetryInterval = t.RetryInterval,
                    Timeout = t.Timeout,
                    NotebookId = t.NotebookId ?? throw new InvalidOperationException("NotebookId is required for notebook tasks."),
                    NodePoolRef = t.NodePoolRef,
                    Parameters = t.Parameters,
                },
                "sqlquery" or "sql" => new SqlQueryTask
                {
                    Id = Guid.NewGuid(),
                    JobId = job.Id,
                    Name = t.Name,
                    MaxRetries = t.MaxRetries,
                    RetryInterval = t.RetryInterval,
                    Timeout = t.Timeout,
                    QueryId = t.QueryId ?? throw new InvalidOperationException("QueryId is required for SQL query tasks."),
                    NodePoolRef = t.NodePoolRef,
                    Parameters = t.Parameters,
                },
                "subjob" => new SubJobTask
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
        for (var i = 0; i < request.Tasks.Count; i++)
        {
            var taskReq = request.Tasks[i];
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
