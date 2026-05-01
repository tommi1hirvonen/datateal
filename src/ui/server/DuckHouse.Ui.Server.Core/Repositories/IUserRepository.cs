using DuckHouse.Core.Users;

namespace DuckHouse.Ui.Server.Core.Repositories;

public interface IUserRepository
{
    Task<IReadOnlyList<AppUser>> GetAllAsync(CancellationToken ct = default);
    Task<AppUser?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<AppUser?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<bool> EmailExistsAsync(string email, Guid? excludeId = null, CancellationToken ct = default);
    Task<AppUser> CreateAsync(AppUser user, CancellationToken ct = default);
    Task<AppUser?> UpdateAsync(Guid id, string displayName, bool isEnabled,
        List<string> roles, bool hasAllCatalogAccess, List<Guid> catalogIds,
        CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}
