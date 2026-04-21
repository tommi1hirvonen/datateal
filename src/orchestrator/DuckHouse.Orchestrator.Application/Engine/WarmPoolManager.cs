using System.Collections.Concurrent;
using DuckHouse.Core.Nodes;
using DuckHouse.Orchestrator.Core.Entities;
using DuckHouse.Orchestrator.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace DuckHouse.Orchestrator.Application.Engine;

/// <summary>
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
public sealed class WarmPoolManager(
    IControlPlaneClient controlPlane,
    IWheelPackageReader wheelPackageReader,
    IEnvironmentResolver environmentResolver,
    ILogger<WarmPoolManager> logger)
{
    private static readonly TimeSpan NodeReadyTimeout = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan NodePollInterval = TimeSpan.FromSeconds(5);
    private const string WarmNodePrefix = "w";

    // Per pool: queue of warm standby entries (ready or still creating)
    private readonly ConcurrentDictionary<Guid, ConcurrentQueue<WarmNodeEntry>> _available = new();
    // Per pool: semaphore enforcing MaxNodes cap (null = unlimited)
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim?> _semaphores = new();

    /// <summary>A warm node entry whose <c>ReadyTask</c> resolves when the node is Running.</summary>
    private sealed record WarmNodeEntry(string NodeName, Task ReadyTask);

    // -- Initialisation --------------------------------------------------------

    /// <summary>
    /// Discovers existing warm nodes from the control plane and seeds each pool's queue.
    /// Called by <see cref="WarmPoolReplenishmentService"/> on startup.
    /// </summary>
    public async Task InitialiseAsync(
        IReadOnlyList<JobNodePoolConfig> configs,
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
                if (semaphore is not null && !semaphore.Wait(0))
                    break;

                if (node.State == NodeState.Running)
                {
                    queue.Enqueue(new WarmNodeEntry(node.Name, Task.CompletedTask));
                }
                else if (node.State == NodeState.Creating)
                {
                    var nodeName = node.Name;
                    var pollTask = PollUntilRunningAsync(nodeName, CancellationToken.None);
                    queue.Enqueue(new WarmNodeEntry(nodeName, pollTask));
                }
                else
                {
                    semaphore?.Release();
                }
            }

            logger.LogInformation(
                "WarmPoolManager: pool '{PoolName}' initialised with {Count} warm node(s) in queue",
                config.Name, queue.Count);
        }
    }

    // -- Node claiming ---------------------------------------------------------

    /// <summary>
    /// Claims a node for a job run.
    /// Dequeues a warm standby when available; falls back to fresh provisioning.
    /// Respects MaxNodes via a semaphore and NodeAcquireTimeout when the cap is reached.
    /// </summary>
    public async Task<string> ClaimNodeAsync(JobNodePoolConfig config, CancellationToken ct)
    {
        EnsurePoolState(config);
        var semaphore = _semaphores[config.Id];
        var queue = _available[config.Id];

        // Try dequeuing an existing warm standby
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

                ScheduleReplenishment(config);
                return entry.NodeName;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "WarmPoolManager: warm node '{NodeName}' (pool '{PoolName}') failed; trying next",
                    entry.NodeName, config.Name);
                semaphore?.Release();
            }
        }

        // No warm standby available -- acquire a slot and create fresh
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
            var freshName = await ProvisionAndWaitAsync(config, warmStandby: false, ct: ct);

            logger.LogInformation(
                "WarmPoolManager: created fresh node '{NodeName}' (pool '{PoolName}', no standby available)",
                freshName, config.Name);

            ScheduleReplenishment(config);
            return freshName;
        }
        catch
        {
            semaphore?.Release();
            throw;
        }
    }

    // -- Node release ----------------------------------------------------------

    /// <summary>
    /// Called when a job run finishes. Deletes the used node and triggers replenishment.
    /// </summary>
    public async Task ReleaseNodeAsync(Guid poolId, string nodeName, JobNodePoolConfig config)
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
            _semaphores.GetValueOrDefault(poolId)?.Release();
        }

        ScheduleReplenishment(config);
    }

    // -- Config adjustments ----------------------------------------------------

    /// <summary>
    /// Called when a pool config is updated via the UI.
    /// Drains excess standbys if WarmNodes was lowered; replenishes if raised.
    /// Rebuilds the semaphore if MaxNodes changed.
    /// </summary>
    public Task AdjustPoolAsync(JobNodePoolConfig newConfig, CancellationToken ct)
    {
        var poolId = newConfig.Id;
        EnsurePoolState(newConfig);

        // Rebuild semaphore when MaxNodes changes.
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

            if (!entry.ReadyTask.IsCompleted)
            {
                queue.Enqueue(entry);
                break;
            }

            logger.LogInformation(
                "WarmPoolManager: draining excess warm node '{NodeName}' (pool '{PoolName}')",
                entry.NodeName, newConfig.Name);

            var capturedEntry = entry;
            var capturedNewSemaphore = newSemaphore;
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
                    capturedNewSemaphore?.Release();
                }
            }, CancellationToken.None);
        }

        ScheduleReplenishment(newConfig);
        return Task.CompletedTask;
    }

    // -- Replenishment ---------------------------------------------------------

    private void ScheduleReplenishment(JobNodePoolConfig config)
    {
        if (config.WarmNodes == 0) return;
        _ = Task.Run(() => ReplenishAsync(config));
    }

    /// <summary>
    /// Creates warm standby nodes until the pool reaches its WarmNodes target.
    /// Called by <see cref="WarmPoolReplenishmentService"/> and by <see cref="ScheduleReplenishment"/>.
    /// </summary>
    public async Task ReplenishAsync(JobNodePoolConfig config)
    {
        EnsurePoolState(config);
        var poolId = config.Id;
        var queue = _available[poolId];
        var semaphore = _semaphores[poolId];

        while (queue.Count < config.WarmNodes)
        {
            if (semaphore is not null && !semaphore.Wait(0))
                break;

            var nodeName = GenerateWarmNodeName(config.Id);
            logger.LogInformation(
                "WarmPoolManager: creating warm standby '{NodeName}' for pool '{PoolName}'",
                nodeName, config.Name);

            var capturedName = nodeName;
            var capturedConfig = config;
            var capturedSemaphore = semaphore;

            var readyTask = Task.Run(async () =>
            {
                try
                {
                    await ProvisionAndWaitAsync(
                        capturedConfig, warmStandby: true, nodeName: capturedName,
                        ct: CancellationToken.None);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "WarmPoolManager: failed to provision warm standby '{NodeName}'",
                        capturedName);
                    capturedSemaphore?.Release();
                    throw;
                }
            });

            queue.Enqueue(new WarmNodeEntry(nodeName, readyTask));
        }
    }

    // -- Internal provisioning -------------------------------------------------

    private async Task<string> ProvisionAndWaitAsync(
        JobNodePoolConfig config,
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

        await PollUntilRunningAsync(nodeName, ct);
        return nodeName;
    }

    private async Task PollUntilRunningAsync(string nodeName, CancellationToken ct)
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