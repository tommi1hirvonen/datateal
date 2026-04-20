using System.Collections.Concurrent;
using DuckHouse.Core.Nodes;
using DuckHouse.Orchestrator.Core.Entities;
using DuckHouse.Orchestrator.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace DuckHouse.Orchestrator.Application.Engine;
/// Singleton that manages warm standby nodes for <see cref="JobNodePoolConfig"/> entries
/// that have <see cref="JobNodePoolConfig.WarmNodes"/> &gt; 0 or
/// <see cref="JobNodePoolConfig.MaxNodes"/> set.
///
/// Each warm node is tracked as a <see cref="WarmNodeEntry"/> whose <c>ReadyTask</c>
/// completes once the node is Running.  A job that claims a still-Creating entry simply
/// awaits that task — faster than cold-starting a completely new node.
///
/// Semaphore slots represent running or being-created node "seats" for the pool.
/// A slot is acquired on node creation and released only when the node is deleted.
/// This makes the semaphore an exact count of total active nodes, enforcing MaxNodes.
/// </summary>
public sealed class WarmPoolManager(ILogger<WarmPoolManager> logger)
{
    private static readonly TimeSpan NodeReadyTimeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan NodePollInterval = TimeSpan.FromSeconds(5);
    private const string WarmNodePrefix = "w";

    // Per pool: queue of warm standby entries (ready or still creating)
    private readonly ConcurrentDictionary<Guid, ConcurrentQueue<WarmNodeEntry>> _available = new();
    // Per pool: semaphore enforcing MaxNodes cap (null = unlimited)
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim?> _semaphores = new();

    /// <summary>A warm node entry whose <c>ReadyTask</c> resolves when the node is Running.</summary>
    private sealed record WarmNodeEntry(string NodeName, Task ReadyTask);

    // ── Initialisation ────────────────────────────────────────────────────────

    /// <summary>
    /// Discovers existing warm nodes from the control plane and seeds each pool's queue.
    /// Called by <see cref="WarmPoolReplenishmentService"/> on startup.
    /// </summary>
    public async Task InitialiseAsync(
        IReadOnlyList<JobNodePoolConfig> configs,
        IControlPlaneClient controlPlane,
        CancellationToken ct)
    {
        IReadOnlyList<NodeInfo> allNodes;
        try
        {
            allNodes = await controlPlane.ListNodesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "WarmPoolManager: failed to list nodes during initialisation; starting with empty queues");
            allNodes = [];
        }

        foreach (var config in configs)
        {
            EnsurePoolState(config);

            var prefix = GetWarmPrefix(config.Id);
            var warmNodes = allNodes
                .Where(n => n.Name.StartsWith(prefix, StringComparison.Ordinal))
                .ToList();

            var queue = _available[config.Id];
            var semaphore = _semaphores[config.Id];

            foreach (var node in warmNodes)
            {
                // Acquire a semaphore slot for each discovered warm node.
                // If MaxNodes is already reached by pre-existing nodes, stop discovering.
                if (semaphore is not null && !semaphore.Wait(0))
                    break;

                if (node.State == NodeState.Running)
                {
                    queue.Enqueue(new WarmNodeEntry(node.Name, Task.CompletedTask));
                }
                else if (node.State == NodeState.Creating)
                {
                    var nodeName = node.Name;
                    var pollTask = PollUntilRunningAsync(nodeName, controlPlane, CancellationToken.None);
                    queue.Enqueue(new WarmNodeEntry(nodeName, pollTask));
                }
                else
                {
                    // Not in a usable state — do not consume a slot.
                    semaphore?.Release();
                }
            }

            logger.LogInformation(
                "WarmPoolManager: pool '{PoolName}' initialised with {Count} warm node(s) in queue",
                config.Name, queue.Count);
        }
    }

    // ── Node claiming ─────────────────────────────────────────────────────────

    /// <summary>
    /// Claims a node for a job run.
    /// <list type="bullet">
    ///   <item>Dequeues a warm standby (ready or still Creating) when available.</item>
    ///   <item>Falls back to creating a fresh node when the queue is empty.</item>
    ///   <item>Respects <see cref="JobNodePoolConfig.MaxNodes"/> via a semaphore.</item>
    ///   <item>Waits up to <see cref="JobNodePoolConfig.NodeAcquireTimeout"/> for a slot
    ///         (<c>null</c> = wait indefinitely).</item>
    /// </list>
    /// </summary>
    public async Task<string> ClaimNodeAsync(
        JobNodePoolConfig config,
        IControlPlaneClient controlPlane,
        IWheelPackageReader wheelPackageReader,
        IEnvironmentResolver environmentResolver,
        CancellationToken ct)
    {
        EnsurePoolState(config);
        var semaphore = _semaphores[config.Id];
        var queue = _available[config.Id];

        // ── Try dequeuing an existing warm standby ────────────────────────────
        // Warm standbys already hold a semaphore slot (acquired during replenishment),
        // so claiming one does NOT require acquiring an additional slot.
        while (queue.TryDequeue(out var entry))
        {
            try
            {
                await entry.ReadyTask; // instant for Running; short wait for Creating

                // Restore normal eviction config (warm nodes are created with Zero timeout)
                await controlPlane.UpdateNodeEvictionConfigAsync(
                    entry.NodeName, config.KernelIdleTimeout, config.NodeIdleTimeout, ct);

                logger.LogInformation(
                    "WarmPoolManager: claimed warm node '{NodeName}' (pool '{PoolName}')",
                    entry.NodeName, config.Name);

                ScheduleReplenishment(config, controlPlane, wheelPackageReader, environmentResolver);
                return entry.NodeName;
            }
            catch (Exception ex)
            {
                // This warm node's provisioning failed — release its slot and try the next entry.
                logger.LogWarning(ex,
                    "WarmPoolManager: warm node '{NodeName}' (pool '{PoolName}') failed; trying next",
                    entry.NodeName, config.Name);
                semaphore?.Release();
            }
        }

        // ── No warm standby available — acquire a slot and create fresh ────────
        if (semaphore is not null)
        {
            var timeout = config.NodeAcquireTimeout ?? Timeout.InfiniteTimeSpan;
            var acquired = await semaphore.WaitAsync(timeout, ct);
            if (!acquired)
                throw new TimeoutException(
                    $"Timed out waiting for an available node in pool '{config.Name}' after " +
                    $"{config.NodeAcquireTimeout}. The pool is at its max node count ({config.MaxNodes}).");
        }

        try
        {
            var freshName = await ProvisionAndWaitAsync(
                config, controlPlane, wheelPackageReader, environmentResolver, warmStandby: false, ct);

            logger.LogInformation(
                "WarmPoolManager: created fresh node '{NodeName}' (pool '{PoolName}', no standby available)",
                freshName, config.Name);

            ScheduleReplenishment(config, controlPlane, wheelPackageReader, environmentResolver);
            return freshName;
        }
        catch
        {
            semaphore?.Release();
            throw;
        }
    }

    // ── Node release ──────────────────────────────────────────────────────────

    /// <summary>
    /// Called when a job run finishes. Deletes the used node (which may have dirty Python
    /// state) and triggers replenishment to restore the warm standby count.
    /// </summary>
    public async Task ReleaseNodeAsync(
        Guid poolId,
        string nodeName,
        JobNodePoolConfig config,
        IControlPlaneClient controlPlane,
        IWheelPackageReader wheelPackageReader,
        IEnvironmentResolver environmentResolver)
    {
        try
        {
            await controlPlane.DeleteNodeAsync(nodeName, CancellationToken.None);
            logger.LogInformation(
                "WarmPoolManager: deleted used node '{NodeName}' (pool '{PoolName}')",
                nodeName, config.Name);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "WarmPoolManager: failed to delete used node '{NodeName}'", nodeName);
        }
        finally
        {
            // Release the slot regardless of delete success so the pool doesn't stall.
            _semaphores.GetValueOrDefault(poolId)?.Release();
        }

        ScheduleReplenishment(config, controlPlane, wheelPackageReader, environmentResolver);
    }

    // ── Config adjustments ────────────────────────────────────────────────────

    /// <summary>
    /// Called when a pool config is updated via the UI.
    /// Drains excess standbys if <see cref="JobNodePoolConfig.WarmNodes"/> was lowered;
    /// replenishes if raised.  Rebuilds the semaphore if MaxNodes changed.
    /// </summary>
    public async Task AdjustPoolAsync(
        JobNodePoolConfig newConfig,
        IControlPlaneClient controlPlane,
        IWheelPackageReader wheelPackageReader,
        IEnvironmentResolver environmentResolver,
        CancellationToken ct)
    {
        var poolId = newConfig.Id;
        EnsurePoolState(newConfig);

        // Rebuild semaphore when MaxNodes changes.
        // In-flight nodes retain their existing slots; the new semaphore only governs new claims.
        var existingSemaphore = _semaphores[poolId];
        SemaphoreSlim? newSemaphore = newConfig.MaxNodes.HasValue
            ? new SemaphoreSlim(newConfig.MaxNodes.Value, newConfig.MaxNodes.Value)
            : null;

        if (existingSemaphore is not null || newSemaphore is not null)
            _semaphores[poolId] = newSemaphore;

        // Drain excess warm standbys when WarmNodes was lowered.
        var queue = _available[poolId];
        var excess = queue.Count - newConfig.WarmNodes;
        for (var i = 0; i < excess; i++)
        {
            if (!queue.TryDequeue(out var entry)) break;

            // Skip still-creating nodes — they're almost ready and will cost money regardless.
            // Put them back and stop draining.
            if (!entry.ReadyTask.IsCompleted)
            {
                queue.Enqueue(entry);
                break;
            }

            logger.LogInformation(
                "WarmPoolManager: draining excess warm node '{NodeName}' (pool '{PoolName}')",
                entry.NodeName, newConfig.Name);

            var capturedEntry = entry;
            _ = Task.Run(async () =>
            {
                try
                {
                    await controlPlane.DeleteNodeAsync(capturedEntry.NodeName, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex,
                        "WarmPoolManager: failed to delete drained node '{NodeName}'",
                        capturedEntry.NodeName);
                }
                finally
                {
                    newSemaphore?.Release();
                }
            }, CancellationToken.None);
        }

        ScheduleReplenishment(newConfig, controlPlane, wheelPackageReader, environmentResolver);
        await Task.CompletedTask;
    }

    // ── Replenishment ─────────────────────────────────────────────────────────

    private void ScheduleReplenishment(
        JobNodePoolConfig config,
        IControlPlaneClient controlPlane,
        IWheelPackageReader wheelPackageReader,
        IEnvironmentResolver environmentResolver)
    {
        if (config.WarmNodes == 0) return;
        _ = Task.Run(() =>
            ReplenishAsync(config, controlPlane, wheelPackageReader, environmentResolver));
    }

    /// <summary>
    /// Creates warm standby nodes until the pool reaches its <see cref="JobNodePoolConfig.WarmNodes"/>
    /// target, subject to <see cref="JobNodePoolConfig.MaxNodes"/>.
    /// </summary>
    public async Task ReplenishAsync(
        JobNodePoolConfig config,
        IControlPlaneClient controlPlane,
        IWheelPackageReader wheelPackageReader,
        IEnvironmentResolver environmentResolver)
    {
        EnsurePoolState(config);
        var poolId = config.Id;
        var queue = _available[poolId];
        var semaphore = _semaphores[poolId];

        while (queue.Count < config.WarmNodes)
        {
            // Try to acquire a slot (non-blocking); if at cap, stop.
            if (semaphore is not null && !semaphore.Wait(0))
                break;

            var nodeName = GenerateWarmNodeName(config.Id);
            logger.LogInformation(
                "WarmPoolManager: creating warm standby '{NodeName}' for pool '{PoolName}'",
                nodeName, config.Name);

            // Capture locals for the async task closure
            var capturedName = nodeName;
            var capturedConfig = config;
            var capturedSemaphore = semaphore;

            var readyTask = Task.Run(async () =>
            {
                try
                {
                    await ProvisionAndWaitAsync(
                        capturedConfig, controlPlane, wheelPackageReader, environmentResolver,
                        warmStandby: true, nodeName: capturedName, ct: CancellationToken.None);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "WarmPoolManager: failed to provision warm standby '{NodeName}'",
                        capturedName);
                    // Release the slot so the pool can try again later.
                    capturedSemaphore?.Release();
                    throw;
                }
            });

            // Enqueue immediately so the next job can claim this in-flight node.
            queue.Enqueue(new WarmNodeEntry(nodeName, readyTask));
        }
    }

    // ── Internal provisioning ─────────────────────────────────────────────────

    private async Task<string> ProvisionAndWaitAsync(
        JobNodePoolConfig config,
        IControlPlaneClient controlPlane,
        IWheelPackageReader wheelPackageReader,
        IEnvironmentResolver environmentResolver,
        bool warmStandby,
        CancellationToken ct,
        string? nodeName = null)
    {
        nodeName ??= GenerateWarmNodeName(config.Id);

        IReadOnlyList<WheelContent>? wheelContents = null;
        if (config.WheelPackageIds is { Count: > 0 })
            wheelContents = await wheelPackageReader.GetWheelContentsAsync(config.WheelPackageIds, ct);

        var resolved = await environmentResolver.ResolveAsync(
            config.EnvironmentVariableIds, config.SecretIds, ct);

        // Warm standby nodes use NodeIdleTimeout = Zero so the eviction service never removes
        // them while they are idle (no kernels).  The timeout is restored when the node is claimed.
        var nodeIdleTimeout = warmStandby ? TimeSpan.Zero : config.NodeIdleTimeout;

        await controlPlane.CreateNodeAsync(
            nodeName,
            config.VmSize,
            config.KernelIdleTimeout,
            nodeIdleTimeout,
            config.KernelRequirements,
            wheelContents,
            resolved.Variables.Count > 0 ? resolved.Variables : null,
            resolved.Secrets.Count > 0 ? resolved.Secrets : null,
            ct);

        await PollUntilRunningAsync(nodeName, controlPlane, ct);
        return nodeName;
    }

    private async Task PollUntilRunningAsync(
        string nodeName,
        IControlPlaneClient controlPlane,
        CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + NodeReadyTimeout;
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(NodePollInterval, ct);

            var node = await controlPlane.GetNodeAsync(nodeName, ct);
            if (node is null) continue;
            if (node.State == NodeState.Running) return;
            if (node.State == NodeState.Failure)
                throw new InvalidOperationException($"Warm node '{nodeName}' failed to provision.");
        }

        throw new TimeoutException(
            $"Warm node '{nodeName}' did not become ready within {NodeReadyTimeout.TotalMinutes} minutes.");
    }

    private void EnsurePoolState(JobNodePoolConfig config)
    {
        _available.GetOrAdd(config.Id, _ => new ConcurrentQueue<WarmNodeEntry>());
        _semaphores.GetOrAdd(config.Id, _ =>
            config.MaxNodes.HasValue
                ? new SemaphoreSlim(config.MaxNodes.Value, config.MaxNodes.Value)
                : null);
    }

    private static string GetWarmPrefix(Guid poolId) =>
        WarmNodePrefix + poolId.ToString("N")[..7];

    private static string GenerateWarmNodeName(Guid poolId)
    {
        var suffix = Convert.ToHexString(Guid.NewGuid().ToByteArray())[..4].ToLowerInvariant();
        return GetWarmPrefix(poolId) + suffix;
    }
}
