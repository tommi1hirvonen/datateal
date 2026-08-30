using Datateal.Orchestrator.Application.Engine;
using Datateal.Orchestrator.Core.Entities;
using Xunit;

namespace Datateal.Core.Tests.Orchestrator;

/// <summary>
/// Coverage for <see cref="JobScheduleSyncCoordinator"/>'s batching behavior: outside a batch,
/// calls reach Quartz (via <see cref="SchedulesManager"/>) immediately; inside a batch they are
/// queued and only take effect once explicitly flushed, and are discarded entirely if the batch
/// scope is disposed without a flush (mirrors a rolled-back deployment transaction).
/// </summary>
public class JobScheduleSyncCoordinatorTests
{
    /// <summary>
    /// Records calls instead of touching a real Quartz scheduler. Constructed with null!
    /// dependencies since <see cref="SchedulesManager"/>'s base implementations of the overridden
    /// methods are never invoked.
    /// </summary>
    private sealed class RecordingSchedulesManager() : SchedulesManager(null!, null!, null!)
    {
        public readonly List<string> Calls = [];

        public override Task AddScheduleAsync(JobSchedule schedule, CancellationToken cancellationToken = default)
        {
            Calls.Add($"add:{schedule.Id}");
            return Task.CompletedTask;
        }

        public override Task RemoveJobAsync(Guid jobId, CancellationToken cancellationToken = default)
        {
            Calls.Add($"remove:{jobId}");
            return Task.CompletedTask;
        }
    }

    private static Job NewJob(params JobSchedule[] schedules)
    {
        var job = new Job { Id = Guid.NewGuid(), WorkspaceId = Guid.NewGuid(), Name = "job" };
        foreach (var s in schedules) job.Schedules.Add(s);
        return job;
    }

    private static JobSchedule NewSchedule() => new() { Id = Guid.NewGuid(), JobId = Guid.NewGuid(), Name = "s", CronExpression = "0 0 * * * ?" };

    [Fact]
    public async Task OutsideBatch_CreatedJob_AppliesImmediately()
    {
        var manager = new RecordingSchedulesManager();
        var coordinator = new JobScheduleSyncCoordinator(manager);
        var schedule = NewSchedule();
        var job = NewJob(schedule);

        await coordinator.OnJobCreatedAsync(job);

        Assert.Equal([$"add:{schedule.Id}"], manager.Calls);
    }

    [Fact]
    public async Task OutsideBatch_DeletedJob_AppliesImmediately()
    {
        var manager = new RecordingSchedulesManager();
        var coordinator = new JobScheduleSyncCoordinator(manager);
        var jobId = Guid.NewGuid();

        await coordinator.OnJobDeletedAsync(jobId);

        Assert.Equal([$"remove:{jobId}"], manager.Calls);
    }

    [Fact]
    public async Task InsideBatch_ActionsAreQueuedNotAppliedUntilFlushed()
    {
        var manager = new RecordingSchedulesManager();
        var coordinator = new JobScheduleSyncCoordinator(manager);
        var schedule = NewSchedule();
        var job = NewJob(schedule);

        using (coordinator.BeginBatch())
        {
            await coordinator.OnJobCreatedAsync(job);
            Assert.Empty(manager.Calls);

            await coordinator.FlushAsync();
        }

        Assert.Equal([$"add:{schedule.Id}"], manager.Calls);
    }

    [Fact]
    public async Task InsideBatch_DisposedWithoutFlush_DiscardsQueuedActions()
    {
        var manager = new RecordingSchedulesManager();
        var coordinator = new JobScheduleSyncCoordinator(manager);
        var job = NewJob(NewSchedule());

        using (coordinator.BeginBatch())
        {
            await coordinator.OnJobCreatedAsync(job);
        }

        // Never flushed (simulates a rolled-back transaction) — Quartz must never observe this.
        Assert.Empty(manager.Calls);

        // A subsequent flush call after the batch has ended must be a no-op (nothing queued).
        await coordinator.FlushAsync();
        Assert.Empty(manager.Calls);
    }

    [Fact]
    public async Task InsideBatch_QueuedActionsFlushInRecordedOrder()
    {
        var manager = new RecordingSchedulesManager();
        var coordinator = new JobScheduleSyncCoordinator(manager);
        var createdJob = NewJob(NewSchedule());
        var deletedJobId = Guid.NewGuid();
        var updatedJob = NewJob(NewSchedule());

        using (coordinator.BeginBatch())
        {
            await coordinator.OnJobCreatedAsync(createdJob);
            await coordinator.OnJobDeletedAsync(deletedJobId);
            await coordinator.OnJobUpdatedAsync(updatedJob);

            await coordinator.FlushAsync();
        }

        Assert.Equal(
        [
            $"add:{createdJob.Schedules[0].Id}",
            $"remove:{deletedJobId}",
            $"remove:{updatedJob.Id}",
            $"add:{updatedJob.Schedules[0].Id}",
        ], manager.Calls);
    }

    [Fact]
    public async Task AfterBatchEnds_SubsequentCallsApplyImmediatelyAgain()
    {
        var manager = new RecordingSchedulesManager();
        var coordinator = new JobScheduleSyncCoordinator(manager);

        using (coordinator.BeginBatch())
        {
            await coordinator.OnJobDeletedAsync(Guid.NewGuid());
        }

        var jobId = Guid.NewGuid();
        await coordinator.OnJobDeletedAsync(jobId);

        Assert.Equal([$"remove:{jobId}"], manager.Calls);
    }
}
