using DuckHouse.Core.Kernels;
using DuckHouse.Core.Nodes;

namespace DuckHouse.Orchestrator.Core.Interfaces;

public interface IControlPlaneClient
{
    // Nodes
    Task<NodeInfo> CreateNodeAsync(
        string name,
        string vmSize,
        TimeSpan? kernelIdleTimeout,
        TimeSpan? nodeIdleTimeout,
        string? kernelRequirements,
        IReadOnlyList<WheelContent>? wheelContents,
        IReadOnlyDictionary<string, string>? environmentVariables,
        IReadOnlyDictionary<string, string>? secrets,
        CancellationToken ct = default);
    Task<IReadOnlyList<NodeInfo>> ListNodesAsync(CancellationToken ct = default);
    Task<NodeInfo?> GetNodeAsync(string name, CancellationToken ct = default);
    Task DeleteNodeAsync(string name, CancellationToken ct = default);
    Task UpdateNodeEvictionConfigAsync(string name, TimeSpan? kernelIdleTimeout, TimeSpan? nodeIdleTimeout, CancellationToken ct = default);

    // Kernels
    Task<KernelInfo> CreateKernelAsync(string nodeName, CancellationToken ct = default);
    Task DeleteKernelAsync(string nodeName, string kernelId, CancellationToken ct = default);
    Task<KernelInfo> RestartKernelAsync(string nodeName, string kernelId, CancellationToken ct = default);
    Task InterruptKernelAsync(string nodeName, string kernelId, CancellationToken ct = default);

    // Execution
    Task<ExecutionHandle> StartExecuteAsync(string nodeName, string kernelId,
        string code, double? timeout = null, CancellationToken ct = default);
    Task<PollExecutionResponse> PollExecutionAsync(string nodeName, string kernelId,
        string executionId, CancellationToken ct = default);
}
