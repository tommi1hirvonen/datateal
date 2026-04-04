using DuckHouse.Core.Nodes;

namespace DuckHouse.Ui.Server.Core.Repositories;

public interface INodeRepository
{
    public Task<IReadOnlyList<NodeInfo>> GetNodesAsync(CancellationToken cancellationToken = default);

    public Task<NodeInfo> CreateNodeAsync(string name, string vmSize, CancellationToken cancellationToken = default);
}
