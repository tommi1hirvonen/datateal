using DuckHouse.Core.Kernels;
using DuckHouse.Core.Nodes;
using DuckHouse.ControlPlane.Core.Repositories;
using DuckHouse.ControlPlane.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DuckHouse.ControlPlane.Application.InactivityEviction;

internal sealed class InactivityEvictionService(
    INodeService nodeService,
    INodeRuntimeClient runtimeClient,
    IServiceScopeFactory scopeFactory,
    IOptions<InactivityEvictionOptions> options,
    ILogger<InactivityEvictionService> logger) : BackgroundService
{
    // High-water mark of the latest kernel LastActivity seen per node.
    // Initialised to UtcNow on first observation so a brand-new node is
    // not immediately considered idle.
    private readonly Dictionary<string, DateTimeOffset> _nodeLastActivity = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = options.Value;

        if (!opts.Enabled)
        {
            logger.LogInformation("Inactivity eviction is disabled.");
            return;
        }

        logger.LogInformation(
            "Inactivity eviction started. CheckInterval={Interval}",
            opts.CheckInterval);

        using var timer = new PeriodicTimer(opts.CheckInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunSweepAsync(stoppingToken);
        }
    }

    private async Task RunSweepAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<NodeInfo> nodes;
        try
        {
            nodes = await nodeService.ListNodesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to list nodes during inactivity sweep.");
            return;
        }

        var now = DateTimeOffset.UtcNow;

        await using var scope = scopeFactory.CreateAsyncScope();
        var nodeConfigRepo = scope.ServiceProvider.GetRequiredService<INodeConfigRepository>();

        foreach (var node in nodes)
        {
            if (node.State != NodeState.Running)
                continue;

            try
            {
                await ProcessNodeAsync(node.Name, now, nodeConfigRepo, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing node {NodeName} during inactivity sweep.", node.Name);
            }
        }

        // Clean up tracking entries for nodes that no longer exist or aren't running.
        var runningNames = nodes.Where(n => n.State == NodeState.Running).Select(n => n.Name).ToHashSet();
        foreach (var name in _nodeLastActivity.Keys.Except(runningNames).ToList())
            _nodeLastActivity.Remove(name);
    }

    private async Task ProcessNodeAsync(
        string nodeName,
        DateTimeOffset now,
        INodeConfigRepository nodeConfigRepo,
        CancellationToken cancellationToken)
    {
        var nodeConfig = await nodeConfigRepo.GetAsync(nodeName, cancellationToken);

        // Fall back to global defaults if no per-node config exists
        // (e.g., nodes created before the DB was introduced).
        var kernelIdleTimeout = nodeConfig?.KernelIdleTimeout ?? options.Value.KernelIdleTimeout;
        var nodeIdleTimeout = nodeConfig?.NodeIdleTimeout ?? options.Value.NodeIdleTimeout;

        var kernels = await runtimeClient.ListKernelsAsync(nodeName, cancellationToken);

        // Update the node's high-water-mark activity from current kernel state.
        if (kernels.Count > 0)
        {
            var latestKernelActivity = kernels.Max(k => k.LastActivity);
            if (!_nodeLastActivity.TryGetValue(nodeName, out var existing) || latestKernelActivity > existing)
                _nodeLastActivity[nodeName] = latestKernelActivity;
        }
        else if (!_nodeLastActivity.ContainsKey(nodeName))
        {
            // First time we observe this node and it has no kernels — treat it as active now.
            _nodeLastActivity[nodeName] = now;
        }

        // Phase 1: evict idle kernels. Never evict a kernel that is not Idle —
        // Busy/Starting/Restarting kernels are actively in use even if LastActivity
        // looks stale (it is set at execution start, not completion).
        foreach (var kernel in kernels)
        {
            if (kernel.Status != KernelStatus.Idle)
                continue;

            if (now - kernel.LastActivity > kernelIdleTimeout)
            {
                logger.LogInformation(
                    "Deleting idle kernel {KernelId} on node {NodeName} (idle for {Idle}).",
                    kernel.Id, nodeName, now - kernel.LastActivity);

                try
                {
                    await runtimeClient.DeleteKernelAsync(nodeName, kernel.Id, cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to delete kernel {KernelId} on node {NodeName}.", kernel.Id, nodeName);
                }
            }
        }

        // Phase 2: stop idle empty node.
        // Re-list kernels; also skip if any kernel is still active (not Idle/Dead).
        var remainingKernels = await runtimeClient.ListKernelsAsync(nodeName, cancellationToken);
        bool anyActive = remainingKernels.Any(k => k.Status is KernelStatus.Busy or KernelStatus.Starting or KernelStatus.Restarting);
        if (!anyActive && remainingKernels.Count == 0 && _nodeLastActivity.TryGetValue(nodeName, out var lastActivity))
        {
            if (now - lastActivity > nodeIdleTimeout)
            {
                logger.LogInformation(
                    "Stopping idle node {NodeName} (no kernels, last activity {Idle} ago).",
                    nodeName, now - lastActivity);

                await nodeService.StopNodeAsync(nodeName, cancellationToken);
                _nodeLastActivity.Remove(nodeName);
            }
        }
    }
}
