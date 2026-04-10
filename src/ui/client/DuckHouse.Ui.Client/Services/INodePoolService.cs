using DuckHouse.Ui.Shared.Orchestration;

namespace DuckHouse.Ui.Client.Services;

public interface INodePoolService
{
    Task<IReadOnlyList<NodePoolConfigDto>> GetNodePoolsAsync(CancellationToken ct = default);
    Task<NodePoolConfigDto> CreateNodePoolAsync(CreateNodePoolRequest request, CancellationToken ct = default);
    Task<NodePoolConfigDto?> UpdateNodePoolAsync(Guid id, UpdateNodePoolRequest request, CancellationToken ct = default);
    Task DeleteNodePoolAsync(Guid id, CancellationToken ct = default);
}
