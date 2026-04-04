using DuckHouse.Core.Nodes;
using DuckHouse.Ui.Server.Core.Repositories;

namespace DuckHouse.Ui.Server.Infrastructure.Repositories;

internal class NodeRepository : INodeRepository
{
    public Task<NodeInfo> CreateNodeAsync(string name, string vmSize, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<IReadOnlyList<NodeInfo>> GetNodesAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
