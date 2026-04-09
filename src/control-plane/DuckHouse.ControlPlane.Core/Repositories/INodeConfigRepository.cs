using DuckHouse.ControlPlane.Core.Nodes;

namespace DuckHouse.ControlPlane.Core.Repositories;

public interface INodeConfigRepository
{
    Task<NodeConfig?> GetAsync(string nodeName, CancellationToken cancellationToken = default);
    Task UpsertAsync(NodeConfig config, CancellationToken cancellationToken = default);
    Task DeleteAsync(string nodeName, CancellationToken cancellationToken = default);
}
