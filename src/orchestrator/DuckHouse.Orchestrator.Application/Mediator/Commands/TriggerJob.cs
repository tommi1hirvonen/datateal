using System.Text.Json;
using DuckHouse.Core.Mediator;
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
    IJobRunRepository jobRunRepository) : IRequestHandler<TriggerJobRequest, JobRun>
{
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
            Status = JobRunStatus.Pending,
            Trigger = request.Trigger,
            ParametersJson = request.Parameters is { Count: > 0 }
                ? JsonSerializer.Serialize(request.Parameters)
                : null,
            CreatedAt = DateTime.UtcNow,
        };

        foreach (var task in job.Tasks)
        {
            run.TaskRuns.Add(new TaskRun
            {
                Id = Guid.NewGuid(),
                JobRunId = run.Id,
                TaskId = task.Id,
                Status = TaskRunStatus.Pending,
                AttemptNumber = 1,
            });
        }

        return await jobRunRepository.CreateJobRunAsync(run, cancellationToken);
    }
}
