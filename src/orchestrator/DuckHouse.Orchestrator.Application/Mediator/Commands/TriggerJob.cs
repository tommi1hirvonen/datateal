using System.Text.Json;
using System.Text.Json.Serialization;
using DuckHouse.Core.Mediator;
using DuckHouse.Orchestrator.Application.Engine;
using DuckHouse.Orchestrator.Core.Entities;
using DuckHouse.Orchestrator.Core.Enums;
using DuckHouse.Orchestrator.Core.Repositories;

namespace DuckHouse.Orchestrator.Application.Mediator.Commands;

public record TriggerJobRequest(
    Guid JobId,
    Dictionary<string, string>? Parameters,
    JobRunTrigger Trigger = JobRunTrigger.Manual) : IRequest<JobRun>;

internal class TriggerJobHandler(
    IJobRepository jobRepository,
    IJobRunRepository jobRunRepository,
    RunDispatcher runDispatcher) : IRequestHandler<TriggerJobRequest, JobRun>
{
    private static readonly JsonSerializerOptions SnapshotOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
    };

    public async Task<JobRun> Handle(TriggerJobRequest request, CancellationToken cancellationToken)
    {
        var job = await jobRepository.GetJobAsync(request.JobId, cancellationToken)
            ?? throw new InvalidOperationException($"Job {request.JobId} not found.");

        var activeCount = await jobRunRepository.GetActiveRunCountAsync(job.Id, cancellationToken);
        if (activeCount >= job.MaxConcurrentRuns)
            throw new InvalidOperationException(
                $"Job '{job.Name}' already has {activeCount} active run(s) (max {job.MaxConcurrentRuns}).");

        var run = new JobRun
        {
            Id = Guid.NewGuid(),
            JobId = job.Id,
            JobName = job.Name,
            Status = JobRunStatus.Pending,
            Trigger = request.Trigger,
            ParametersJson = request.Parameters is { Count: > 0 }
                ? JsonSerializer.Serialize(request.Parameters)
                : null,
            SnapshotJson = JsonSerializer.Serialize(job, SnapshotOptions),
            CreatedAt = DateTime.UtcNow,
        };

        foreach (var task in job.Tasks)
        {
            string taskType = task switch
            {
                NotebookTask => "Notebook",
                SqlQueryTask => "SqlQuery",
                SubJobTask => "SubJob",
                _ => task.GetType().Name,
            };

            run.TaskRuns.Add(new TaskRun
            {
                Id = Guid.NewGuid(),
                JobRunId = run.Id,
                TaskId = task.Id,
                TaskName = task.Name,
                TaskType = taskType,
                Status = TaskRunStatus.Pending,
                AttemptNumber = 1,
            });
        }

        var created = await jobRunRepository.CreateJobRunAsync(run, cancellationToken);

        // Dispatch the run for background execution
        runDispatcher.DispatchRun(created.Id);

        return created;
    }
}
