using DuckHouse.Core.Nodes;

namespace DuckHouse.Ui.Client.Services;

public interface INodeService
{
    Task<IReadOnlyList<NodeInfo>> GetNodesAsync(CancellationToken cancellationToken = default);
    Task<NodeInfo?> GetNodeAsync(string name, CancellationToken cancellationToken = default);
    Task RemoveNodeAsync(string name, CancellationToken cancellationToken = default);
}
