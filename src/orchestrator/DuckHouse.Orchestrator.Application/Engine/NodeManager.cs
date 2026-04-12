using System.Collections.Concurrent;
using DuckHouse.Core.Nodes;
using DuckHouse.Orchestrator.Core.Interfaces;
using DuckHouse.Orchestrator.Core.Repositories;
using Microsoft.Extensions.Logging;

namespace DuckHouse.Orchestrator.Application.Engine;

/// <summary>
/// Manages node provisioning and cleanup for a single job run.
/// Each NodePoolRef maps to one shared node; multiple tasks can share it.
/// </summary>
public class NodeManager(
    IControlPlaneClient controlPlane,
    INodePoolConfigRepository nodePoolConfigRepo,
    Guid jobRunId,
    ILogger logger)
{
    private readonly ConcurrentDictionary<string, NodeAllocation> _allocations = new();
    private static readonly TimeSpan NodeReadyTimeout = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan NodePollInterval = TimeSpan.FromSeconds(5);

    private record NodeAllocation(string NodeName, bool Provisioned);

    /// <summary>
    /// Ensures a node is provisioned and running for the given NodePoolRef.
    /// Returns the node name. Provisions on first call, returns cached on subsequent calls.
    /// </summary>
    public async Task<string> EnsureNodeAsync(string nodePoolRef, CancellationToken ct)
    {
        if (_allocations.TryGetValue(nodePoolRef, out var existing))
            return existing.NodeName;

        var config = await nodePoolConfigRepo.GetByNameAsync(nodePoolRef, ct)
            ?? throw new InvalidOperationException(
                $"Node pool configuration '{nodePoolRef}' not found.");

        var nodeName = $"job-{jobRunId.ToString()[..8]}-{nodePoolRef}".ToLowerInvariant();

        if (nodeName.Length > 63)
            throw new InvalidOperationException(
                $"Generated node name '{nodeName}' exceeds the 63-character Kubernetes limit. " +
                $"Node pool name '{nodePoolRef}' must be {63 - 13} characters or fewer.");
        logger.LogInformation("Provisioning node '{NodeName}' for pool '{PoolRef}' (VM: {VmSize})",
            nodeName, nodePoolRef, config.VmSize);

        await controlPlane.CreateNodeAsync(
            nodeName, config.VmSize,
            config.KernelIdleTimeout, config.NodeIdleTimeout, ct);

        // Poll until node is running
        var deadline = DateTime.UtcNow + NodeReadyTimeout;
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(NodePollInterval, ct);

            var node = await controlPlane.GetNodeAsync(nodeName, ct);
            if (node is null) continue;

            if (node.State == NodeState.Running)
            {
                logger.LogInformation("Node '{NodeName}' is running", nodeName);
                _allocations[nodePoolRef] = new NodeAllocation(nodeName, true);
                return nodeName;
            }

            if (node.State == NodeState.Failure)
                throw new InvalidOperationException(
                    $"Node '{nodeName}' failed to provision.");
        }

        throw new TimeoutException(
            $"Node '{nodeName}' did not become ready within {NodeReadyTimeout.TotalMinutes} minutes.");
    }

    /// <summary>
    /// Creates a kernel on the node associated with the given NodePoolRef.
    /// </summary>
    public async Task<(string nodeName, string kernelId)> CreateKernelAsync(
        string nodePoolRef, CancellationToken ct)
    {
        var nodeName = await EnsureNodeAsync(nodePoolRef, ct);
        var kernel = await controlPlane.CreateKernelAsync(nodeName, ct);
        logger.LogInformation("Created kernel '{KernelId}' on node '{NodeName}'",
            kernel.Id, nodeName);
        return (nodeName, kernel.Id);
    }

    /// <summary>
    /// Deletes a specific kernel.
    /// </summary>
    public async Task CleanupKernelAsync(string nodeName, string kernelId, CancellationToken ct)
    {
        try
        {
            await controlPlane.DeleteKernelAsync(nodeName, kernelId, ct);
            logger.LogInformation("Deleted kernel '{KernelId}' on node '{NodeName}'",
                kernelId, nodeName);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete kernel '{KernelId}' on node '{NodeName}'",
                kernelId, nodeName);
        }
    }

    /// <summary>
    /// Cleans up all nodes provisioned for this job run.
    /// </summary>
    public async Task CleanupAllAsync(CancellationToken ct)
    {
        foreach (var (poolRef, alloc) in _allocations)
        {
            if (!alloc.Provisioned) continue;
            try
            {
                logger.LogInformation("Deleting node '{NodeName}' (pool '{PoolRef}')",
                    alloc.NodeName, poolRef);
                await controlPlane.DeleteNodeAsync(alloc.NodeName, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to delete node '{NodeName}'", alloc.NodeName);
            }
        }
        _allocations.Clear();
    }
}
