using DuckHouse.Core.Nodes;

namespace DuckHouse.Ui.Server.Core.Repositories;

public interface INodeRepository
{
    Task<IReadOnlyList<NodeInfo>> GetNodesAsync(CancellationToken cancellationToken = default);
    Task<NodeInfo?> GetNodeAsync(string name, CancellationToken cancellationToken = default);
    Task<NodeInfo> CreateNodeAsync(
        string name,
        string vmSize,
        TimeSpan? kernelIdleTimeout = null,
        TimeSpan? nodeIdleTimeout = null,
        CancellationToken cancellationToken = default);
    Task RemoveNodeAsync(string name, CancellationToken cancellationToken = default);
    Task StopNodeAsync(string name, CancellationToken cancellationToken = default);
    Task StartNodeAsync(string name, CancellationToken cancellationToken = default);
}
