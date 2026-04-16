using DuckHouse.Core.RuntimePackages;

namespace DuckHouse.Ui.Server.Core.Repositories;

public interface IWheelPackageRepository
{
    Task<IReadOnlyList<WheelPackage>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<WheelPackage?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WheelPackage>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
    Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<WheelPackage> AddAsync(WheelPackage package, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
