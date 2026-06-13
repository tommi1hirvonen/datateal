using System.Text.Json;
using System.Text.Json.Serialization;
using Datateal.Core.Mediator;
using Datateal.Core.Orchestration;
using Datateal.Orchestrator.Application.Engine;
using Datateal.Orchestrator.Core.Entities;
using Datateal.Orchestrator.Core.Enums;
using Datateal.Orchestrator.Core.Interfaces;
using Datateal.Orchestrator.Core.Repositories;

namespace Datateal.Orchestrator.Application.Mediator.Commands;

public record TriggerJobRequest(
    Guid WorkspaceId,
    Guid JobId,
    Dictionary<string, string>? Parameters,
    JobRunTrigger Trigger = JobRunTrigger.Manual) : IRequest<JobRun?>;

internal class TriggerJobHandler(
    IJobRepository jobRepository,
    IJobRunRepository jobRunRepository,
    IWorkspaceReader workspaceReader,
    ICatalogAccessAuthorizer catalogAuthorizer,
    RunDispatcher runDispatcher) : IRequestHandler<TriggerJobRequest, JobRun?>
{
    private static readonly JsonSerializerOptions SnapshotOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
    };

    /// <summary>
    /// Merges caller-supplied overrides with schema defaults.
    /// Caller overrides take precedence; defaults fill in any schema parameter that has no override.
    /// </summary>
    private static Dictionary<string, string> BuildEffectiveParameters(
        IEnumerable<JobParameter> schema,
        Dictionary<string, string>? overrides)
    {
        var result = overrides is not null
            ? new Dictionary<string, string>(overrides, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var param in schema)
        {
            if (param.DefaultValue is not null && !result.ContainsKey(param.Name))
                result[param.Name] = param.DefaultValue;
        }

        return result;
    }

    /// <summary>
    /// Validates that the job's effective owner is authorized to access every catalog referenced by the
    /// job's notebook/SQL tasks. Throws (rejecting the trigger) when no owner is set or any catalog is
    /// inaccessible. Sub-job tasks are validated when their own child run is triggered.
    /// </summary>
    private async Task EnsureOwnerCatalogAccessAsync(Job job, CancellationToken ct)
    {
        var itemIds = job.Tasks
            .Select(t => t switch
            {
                NotebookTask n => (Guid?)n.NotebookId,
                SqlQueryTask q => (Guid?)q.QueryId,
                _ => null,
            })
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var catalogNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var itemId in itemIds)
        {
            foreach (var name in await workspaceReader.GetWorkspaceItemCatalogNamesAsync(itemId, ct))
                catalogNames.Add(name);
        }

        if (catalogNames.Count == 0)
            return;

        if (job.OwnerUserId is null)
            throw new InvalidOperationException(
                $"Job '{job.Name}' has no effective owner; cannot authorize catalog access. Re-save the " +
                "job as a provisioned user to set an owner before running it.");

        var inaccessible = await catalogAuthorizer.GetInaccessibleAsync(
            job.OwnerUserId.Value, job.WorkspaceId, catalogNames.ToList(), ct);
        if (inaccessible.Count > 0)
            throw new InvalidOperationException(
                $"The owner of job '{job.Name}' is not authorized to access catalog(s): " +
                $"{string.Join(", ", inaccessible)}.");
    }

    public async Task<JobRun?> Handle(TriggerJobRequest request, CancellationToken cancellationToken)
    {
        var job = await jobRepository.GetJobAsync(request.JobId, cancellationToken);
        if (job is null || job.WorkspaceId != request.WorkspaceId) return null;

        var activeCount = await jobRunRepository.GetActiveRunCountAsync(job.Id, cancellationToken);
        if (activeCount >= job.MaxConcurrentRuns)
            throw new InvalidOperationException(
                $"Job '{job.Name}' already has {activeCount} active run(s) (max {job.MaxConcurrentRuns}).");

        // Merge caller-supplied overrides with schema defaults, then validate required parameters.
        var effectiveParameters = BuildEffectiveParameters(job.Parameters, request.Parameters);
        var missing = job.Parameters
            .Where(p => p.IsRequired && !effectiveParameters.ContainsKey(p.Name))
            .Select(p => p.Name)
            .ToList();
        if (missing.Count > 0)
            throw new InvalidOperationException(
                $"Job '{job.Name}' is missing required parameter(s): {string.Join(", ", missing)}.");

        // Authorize catalog access for the job's effective owner before creating the run, so a job
        // referencing catalogs the owner cannot access is rejected up front rather than failing mid-DAG.
        await EnsureOwnerCatalogAccessAsync(job, cancellationToken);

        var run = new JobRun
        {
            Id = Guid.NewGuid(),
            JobId = job.Id,
            WorkspaceId = job.WorkspaceId,
            OwnerUserId = job.OwnerUserId,
            JobName = job.Name,
            Status = JobRunStatus.Pending,
            Trigger = request.Trigger,
            ParametersJson = effectiveParameters.Count > 0
                ? JsonSerializer.Serialize(effectiveParameters)
                : null,
            SnapshotJson = JsonSerializer.Serialize(job, SnapshotOptions),
            CreatedAt = DateTime.UtcNow,
        };

        foreach (var task in job.Tasks)
        {
            TaskRun taskRun = task switch
            {
                NotebookTask => new NotebookTaskRun(),
                SqlQueryTask => new SqlQueryTaskRun(),
                SubJobTask => new SubJobTaskRun(),
                _ => throw new InvalidOperationException($"Unknown task type: {task.GetType().Name}"),
            };

            taskRun.Id = Guid.NewGuid();
            taskRun.JobRunId = run.Id;
            taskRun.TaskId = task.Id;
            taskRun.TaskName = task.Name;
            taskRun.Status = TaskRunStatus.Pending;
            taskRun.AttemptNumber = 1;
            taskRun.Parameters = task switch
            {
                NotebookTask t => t.Parameters,
                SqlQueryTask t => t.Parameters,
                SubJobTask t => t.Parameters,
                _ => null,
            };

            run.TaskRuns.Add(taskRun);
        }

        var created = await jobRunRepository.CreateJobRunAsync(run, cancellationToken);

        // Dispatch the run for background execution
        runDispatcher.DispatchRun(created.Id);

        return created;
    }
}
