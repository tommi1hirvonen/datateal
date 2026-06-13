using Datateal.Core.Mediator;
using Datateal.Core.Orchestration;
using Datateal.Orchestrator.Application.Validation;
using Datateal.Orchestrator.Core.Entities;
using Datateal.Orchestrator.Core.Enums;
using Datateal.Orchestrator.Core.Repositories;

namespace Datateal.Orchestrator.Application.Mediator.Commands;

public record UpdateJobRequest(
    Guid WorkspaceId,
    Guid Id,
    string Name,
    string? Description,
    Guid? FolderId,
    int MaxConcurrentRuns,
    bool IsEnabled,
    List<UpdateJobTaskRequest>? Tasks = null,
    List<UpdateJobParameterRequest>? Parameters = null) : IRequest<Job?>;

public record UpdateJobTaskRequest(
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
    List<UpdateJobDependencyRequest> Dependencies);

public record UpdateJobDependencyRequest(string DependsOnTaskName, DependencyCondition Condition);

public record UpdateJobParameterRequest(string Name, string? DefaultValue, bool IsRequired, string? Description);

internal class UpdateJobHandler(IJobRepository jobRepository) : IRequestHandler<UpdateJobRequest, Job?>
{
    public async Task<Job?> Handle(UpdateJobRequest request, CancellationToken cancellationToken)
    {
        var existing = await jobRepository.GetJobAsync(request.Id, cancellationToken);
        if (existing is null) return null;
        if (existing.WorkspaceId != request.WorkspaceId) return null;

        // Validate unique task names in the submitted task list.
        var taskNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in request.Tasks ?? [])
        {
            if (!taskNames.Add(t.Name))
                throw new InvalidOperationException($"Duplicate task name: \"{t.Name}\". Task names must be unique within a job.");
        }

        // Validate unique job name within the workspace — exclude this job from the check.
        if (!string.Equals(existing.Name, request.Name, StringComparison.OrdinalIgnoreCase))
        {
            var nameConflict = await jobRepository.GetJobByNameAsync(request.Name, existing.WorkspaceId, cancellationToken);
            if (nameConflict is not null)
                throw new JobNameConflictException(request.Name);
        }

        // Update top-level settings
        existing.Name = request.Name;
        existing.Description = request.Description;
        existing.FolderId = request.FolderId;
        existing.MaxConcurrentRuns = request.MaxConcurrentRuns;
        existing.IsEnabled = request.IsEnabled;

        // Replace parameters if provided
        // NOTE: New entities must NOT have pre-set IDs (leave as Guid.Empty) so that
        // EF Core marks them as Added when attached via the tracked navigation collection.
        // Entities with non-default keys would be attached as Unchanged/Modified and EF
        // would try to UPDATE non-existent rows → DbUpdateConcurrencyException.
        if (request.Parameters is not null)
        {
            existing.Parameters.Clear();
            foreach (var p in request.Parameters)
            {
                ParameterNameValidator.Validate(p.Name);
                existing.Parameters.Add(new JobParameter
                {
                    JobId = existing.Id,
                    Name = p.Name,
                    DefaultValue = p.DefaultValue,
                    IsRequired = p.IsRequired,
                    Description = p.Description,
                });
            }
        }

        // Replace tasks if provided
        if (request.Tasks is not null)
        {
            existing.Tasks.Clear();
            var tasksByName = new Dictionary<string, JobTask>(StringComparer.OrdinalIgnoreCase);

            foreach (var t in request.Tasks)
            {
                JobTask task = t.TaskType switch
                {
                    TaskType.Notebook => new NotebookTask
                    {
                        JobId = existing.Id,
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
                        JobId = existing.Id,
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
                        JobId = existing.Id,
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
                existing.Tasks.Add(task);
            }

            // Resolve dependencies by task name — use navigation property (not FK)
            // because the new tasks have default (Guid.Empty) IDs at this point.
            for (var i = 0; i < request.Tasks.Count; i++)
            {
                var taskReq = request.Tasks[i];
                var task = existing.Tasks[i];

                foreach (var dep in taskReq.Dependencies)
                {
                    if (!tasksByName.TryGetValue(dep.DependsOnTaskName, out var dependsOnTask))
                        throw new InvalidOperationException($"Task '{task.Name}' depends on unknown task '{dep.DependsOnTaskName}'.");

                    task.Dependencies.Add(new TaskDependency
                    {
                        DependsOnTask = dependsOnTask,
                        Condition = dep.Condition,
                    });
                }
            }

            DagValidator.Validate(existing.Tasks);
        }

        return await jobRepository.UpdateJobAsync(existing, cancellationToken);
    }
}
