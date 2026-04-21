using DuckHouse.Orchestrator.Core.Entities;
using DuckHouse.Orchestrator.Core.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Impl.Matchers;

namespace DuckHouse.Orchestrator.Application.Engine;

/// <summary>
/// Singleton background service that manages Quartz cron triggers for all job schedules.
/// Loads all schedules from the database on startup and updates the in-memory Quartz scheduler
/// immediately when schedules are created, updated, or deleted.
/// </summary>
public class SchedulesManager(
    ISchedulerFactory schedulerFactory,
    IServiceScopeFactory scopeFactory,
    ILogger<SchedulesManager> logger) : BackgroundService
{
    private IScheduler _scheduler = null!;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _scheduler = await schedulerFactory.GetScheduler(stoppingToken);

        try
        {
            await ReadAllSchedulesAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load schedules from database on startup");
        }

        // Keep the background service alive until the host shuts down.
        await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

        logger.LogInformation("SchedulesManager stopping");
    }

    /// <summary>
    /// Reloads all schedules from the database, replacing any existing Quartz jobs and triggers.
    /// </summary>
    public async Task ReadAllSchedulesAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            logger.LogInformation("Loading all schedules from database");

            using var scope = scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IScheduleRepository>();
            var schedules = await repo.GetAllSchedulesAsync(cancellationToken);

            await _scheduler.Clear(cancellationToken);

            var count = 0;
            foreach (var schedule in schedules)
            {
                try
                {
                    await CreateAndAddScheduleAsync(schedule, cancellationToken);
                    count++;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error registering schedule {ScheduleId} with Quartz", schedule.Id);
                }
            }

            logger.LogInformation("{Loaded}/{Total} schedules loaded successfully", count, schedules.Count);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Registers a newly created schedule with the Quartz scheduler.
    /// </summary>
    public async Task AddScheduleAsync(JobSchedule schedule, CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            await CreateAndAddScheduleAsync(schedule, cancellationToken);
            logger.LogInformation("Added schedule {ScheduleId} for job {JobId} (cron: {Cron}, enabled: {Enabled})",
                schedule.Id, schedule.JobId, schedule.CronExpression, schedule.IsEnabled);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Updates an existing schedule in the Quartz scheduler by removing and re-adding it.
    /// </summary>
    public async Task UpdateScheduleAsync(JobSchedule schedule, CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            var jobKey = new JobKey(schedule.Id.ToString(), schedule.JobId.ToString());
            await _scheduler.DeleteJob(jobKey, cancellationToken);
            await CreateAndAddScheduleAsync(schedule, cancellationToken);
            logger.LogInformation("Updated schedule {ScheduleId} for job {JobId} (cron: {Cron}, enabled: {Enabled})",
                schedule.Id, schedule.JobId, schedule.CronExpression, schedule.IsEnabled);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Removes a single schedule from the Quartz scheduler.
    /// </summary>
    public async Task RemoveScheduleAsync(JobSchedule schedule, CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            var jobKey = new JobKey(schedule.Id.ToString(), schedule.JobId.ToString());
            await _scheduler.DeleteJob(jobKey, cancellationToken);
            logger.LogInformation("Removed schedule {ScheduleId} for job {JobId}", schedule.Id, schedule.JobId);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Removes all Quartz triggers associated with a job. Called when a job is deleted.
    /// </summary>
    public async Task RemoveJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            var matcher = GroupMatcher<JobKey>.GroupEquals(jobId.ToString());
            var jobKeys = await _scheduler.GetJobKeys(matcher, cancellationToken);
            if (jobKeys.Count > 0)
            {
                await _scheduler.DeleteJobs(jobKeys, cancellationToken);
                logger.LogInformation("Removed {Count} Quartz trigger(s) for deleted job {JobId}", jobKeys.Count, jobId);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Returns the next UTC fire time for a given schedule, or null if no trigger is registered.
    /// </summary>
    public async Task<DateTime?> GetNextFireTimeAsync(Guid scheduleId, CancellationToken cancellationToken = default)
    {
        var triggerKey = new TriggerKey(scheduleId.ToString());
        var trigger = await _scheduler.GetTrigger(triggerKey, cancellationToken);
        return trigger?.GetNextFireTimeUtc()?.UtcDateTime;
    }

    private async Task CreateAndAddScheduleAsync(JobSchedule schedule, CancellationToken cancellationToken)
    {
        var jobKey = new JobKey(schedule.Id.ToString(), schedule.JobId.ToString());
        var triggerKey = new TriggerKey(schedule.Id.ToString());

        var jobDetail = JobBuilder.Create<ScheduledJobExecutor>()
            .WithIdentity(jobKey)
            .Build();

        var tz = !string.IsNullOrWhiteSpace(schedule.TimeZone)
            ? TryFindTimeZone(schedule.TimeZone) ?? TimeZoneInfo.Local
            : TimeZoneInfo.Local;

        var trigger = TriggerBuilder.Create()
            .WithIdentity(triggerKey)
            .ForJob(jobDetail)
            .WithCronSchedule(schedule.CronExpression, x => x
                .InTimeZone(tz)
                .WithMisfireHandlingInstructionDoNothing())
            .Build();

        await _scheduler.ScheduleJob(jobDetail, trigger, cancellationToken);

        if (!schedule.IsEnabled)
        {
            await _scheduler.PauseTrigger(triggerKey, cancellationToken);
        }
    }

    private TimeZoneInfo? TryFindTimeZone(string id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Unknown timezone '{TimeZone}', falling back to server local time", id);
            return null;
        }
    }
}
