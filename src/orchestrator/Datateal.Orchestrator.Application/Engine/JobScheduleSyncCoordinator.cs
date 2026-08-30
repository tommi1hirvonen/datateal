using Datateal.Orchestrator.Core.Entities;

namespace Datateal.Orchestrator.Application.Engine;

/// <summary>
/// Mediates between job Create/Update/Delete handlers and <see cref="SchedulesManager"/>.
///
/// Outside of a batch, calls are forwarded to Quartz immediately (the existing behavior for
/// ordinary single-job API calls). Inside a batch (see <see cref="BeginBatch"/>), calls are
/// queued instead of executed immediately, so that callers driving multiple job changes inside
/// a single database transaction (e.g. deployment apply) can defer all Quartz mutations until
/// the transaction has actually committed — Quartz has no rollback mechanism, so it must never
/// be mutated for a DB change that might still be rolled back.
/// </summary>
public interface IJobScheduleSyncCoordinator
{
    Task OnJobCreatedAsync(Job job, CancellationToken cancellationToken = default);

    Task OnJobUpdatedAsync(Job job, CancellationToken cancellationToken = default);

    Task OnJobDeletedAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a batch: subsequent calls on this coordinator instance are queued instead of
    /// applied immediately. Dispose the returned scope to end the batch (any actions not
    /// flushed via <see cref="FlushAsync"/> before disposal are discarded).
    /// </summary>
    IDisposable BeginBatch();

    /// <summary>
    /// Replays all queued actions, in the order they were recorded, against Quartz. Must only be
    /// called after the enclosing database transaction has committed successfully. Clears the
    /// queue afterward regardless of outcome.
    /// </summary>
    Task FlushAsync(CancellationToken cancellationToken = default);
}

internal sealed class JobScheduleSyncCoordinator(SchedulesManager schedulesManager) : IJobScheduleSyncCoordinator
{
    private readonly List<Func<CancellationToken, Task>> _queuedActions = [];
    private int _batchDepth;

    public Task OnJobCreatedAsync(Job job, CancellationToken cancellationToken = default) =>
        DispatchAsync(ct => AddSchedulesAsync(job, ct), cancellationToken);

    public Task OnJobUpdatedAsync(Job job, CancellationToken cancellationToken = default) =>
        DispatchAsync(ct => ReplaceSchedulesAsync(job, ct), cancellationToken);

    public Task OnJobDeletedAsync(Guid jobId, CancellationToken cancellationToken = default) =>
        DispatchAsync(ct => schedulesManager.RemoveJobAsync(jobId, ct), cancellationToken);

    public IDisposable BeginBatch()
    {
        _batchDepth++;
        return new BatchScope(this);
    }

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        var actions = _queuedActions.ToArray();
        _queuedActions.Clear();
        foreach (var action in actions)
        {
            await action(cancellationToken);
        }
    }

    private Task DispatchAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken)
    {
        if (_batchDepth > 0)
        {
            _queuedActions.Add(action);
            return Task.CompletedTask;
        }

        return action(cancellationToken);
    }

    private async Task AddSchedulesAsync(Job job, CancellationToken cancellationToken)
    {
        foreach (var schedule in job.Schedules)
            await schedulesManager.AddScheduleAsync(schedule, cancellationToken);
    }

    private async Task ReplaceSchedulesAsync(Job job, CancellationToken cancellationToken)
    {
        // Mirrors the pre-existing handler behavior: remove all previously-registered triggers for
        // the job, then re-add the current set. Simpler and safer than diffing since schedule IDs
        // are freshly regenerated on every update (see UpdateJobHandler).
        await schedulesManager.RemoveJobAsync(job.Id, cancellationToken);
        foreach (var schedule in job.Schedules)
            await schedulesManager.AddScheduleAsync(schedule, cancellationToken);
    }

    private sealed class BatchScope(JobScheduleSyncCoordinator owner) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            owner._batchDepth--;
            if (owner._batchDepth == 0)
            {
                // Any actions never flushed by the caller (e.g. because the transaction was
                // rolled back) must not be applied.
                owner._queuedActions.Clear();
            }
        }
    }
}
