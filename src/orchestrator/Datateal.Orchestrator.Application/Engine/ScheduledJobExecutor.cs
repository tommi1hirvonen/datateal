using Datateal.Core.Mediator;
using Datateal.Core.Orchestration;
using Datateal.Orchestrator.Application.Mediator.Commands;
using Datateal.Orchestrator.Core.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Datateal.Orchestrator.Application.Engine;

/// <summary>
/// Quartz IJob implementation that fires when a cron trigger is due.
/// Checks whether the job is enabled, then triggers a new run via the mediator.
/// The mediator handler (<see cref="TriggerJobHandler"/>) handles DB persistence and dispatch.
/// </summary>
public class ScheduledJobExecutor(
    IServiceScopeFactory scopeFactory,
    ILogger<ScheduledJobExecutor> logger) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var jobId = Guid.Parse(context.JobDetail.Key.Group);
        var scheduleId = Guid.Parse(context.Trigger.Key.Name);

        try
        {
            using var scope = scopeFactory.CreateScope();
            var jobRepo = scope.ServiceProvider.GetRequiredService<IJobRepository>();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            var job = await jobRepo.GetJobAsync(jobId, context.CancellationToken);
            if (job is null)
            {
                logger.LogWarning("Scheduled fire skipped: job {JobId} not found", jobId);
                return;
            }

            if (!job.IsEnabled)
            {
                logger.LogDebug("Scheduled fire skipped: job '{JobName}' ({JobId}) is disabled", job.Name, jobId);
                return;
            }

            var schedule = job.Schedules.FirstOrDefault(s => s.Id == scheduleId);
            if (schedule is null)
            {
                logger.LogWarning("Scheduled fire skipped: schedule {ScheduleId} not found on job {JobId}", scheduleId, jobId);
                return;
            }

            logger.LogInformation(
                "Schedule {ScheduleId} firing for job '{JobName}' ({JobId})",
                scheduleId, job.Name, jobId);

            // TriggerJobRequest handler handles both DB persistence and dispatch via RunDispatcher.
            var run = await mediator.SendAsync(
                new TriggerJobRequest(job.WorkspaceId, jobId, schedule.Parameters, JobRunTrigger.Scheduled),
                context.CancellationToken)
                ?? throw new InvalidOperationException($"Job {jobId} not found.");

            logger.LogInformation(
                "Run {RunId} triggered for job '{JobName}' ({JobId}) via schedule {ScheduleId}",
                run.Id, job.Name, jobId, scheduleId);
        }
        catch (Exception ex)
        {
            // Never let an exception propagate back to Quartz's thread pool.
            logger.LogError(ex,
                "Error executing scheduled job for job {JobId}, schedule {ScheduleId}",
                jobId, scheduleId);
        }
    }
}
