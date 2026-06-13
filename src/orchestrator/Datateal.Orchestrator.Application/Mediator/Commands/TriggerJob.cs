using System.Text.Json;
using System.Text.Json.Serialization;
using Datateal.Core.Mediator;
using Datateal.Core.Orchestration;
using Datateal.Orchestrator.Application.Engine;
using Datateal.Orchestrator.Core.Entities;
using Datateal.Orchestrator.Core.Enums;
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

        var run = new JobRun
        {
            Id = Guid.NewGuid(),
            JobId = job.Id,
            WorkspaceId = job.WorkspaceId,
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
