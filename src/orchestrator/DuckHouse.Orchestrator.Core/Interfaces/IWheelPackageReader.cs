using DuckHouse.Core.Nodes;

namespace DuckHouse.Orchestrator.Core.Interfaces;

public interface IWheelPackageReader
{
    Task<IReadOnlyList<WheelContent>> GetWheelContentsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);
}
