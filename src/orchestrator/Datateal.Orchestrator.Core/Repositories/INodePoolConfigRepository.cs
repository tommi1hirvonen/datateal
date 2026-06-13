using Datateal.Orchestrator.Core.Entities;

namespace Datateal.Orchestrator.Core.Repositories;

public interface INodePoolConfigRepository
{
    Task<IReadOnlyList<NodePoolConfig>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<NodePoolConfig?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<NodePoolConfig?> GetByNameAsync(string name, Guid workspaceId, CancellationToken cancellationToken = default);
    Task<NodePoolConfig> CreateAsync(NodePoolConfig config, CancellationToken cancellationToken = default);
    Task<NodePoolConfig?> UpdateAsync(NodePoolConfig config, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
