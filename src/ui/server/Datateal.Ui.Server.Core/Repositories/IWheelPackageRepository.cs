using Datateal.Core.RuntimePackages;

namespace Datateal.Ui.Server.Core.Repositories;

public interface IWheelPackageRepository
{
    Task<IReadOnlyList<WheelPackage>> GetAllAsync(Guid workspaceId, CancellationToken cancellationToken = default);
    Task<WheelPackage?> GetByIdAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WheelPackage>> GetByIdsAsync(Guid workspaceId, IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
    Task<bool> ExistsByNameAsync(Guid workspaceId, string name, CancellationToken cancellationToken = default);
    Task<WheelPackage> AddAsync(Guid workspaceId, WheelPackage package, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken = default);
}
