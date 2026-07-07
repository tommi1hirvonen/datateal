using Datateal.Core.Users;

namespace Datateal.Ui.Server.Core.Repositories;

public interface IUserRepository
{
    /// <summary>Returns all interactive <see cref="UserAccount"/> entries (excludes service accounts).</summary>
    Task<IReadOnlyList<UserAccount>> GetAllUserAccountsAsync(CancellationToken ct = default);

    /// <summary>Returns all <see cref="ServiceAccount"/> entries.</summary>
    Task<IReadOnlyList<ServiceAccount>> GetAllServiceAccountsAsync(CancellationToken ct = default);

    Task<AppUser?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<AppUser?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<bool> EmailExistsAsync(string email, Guid? excludeId = null, CancellationToken ct = default);

    /// <summary>Batch-fetches any <see cref="AppUser"/> (user account or service account) by IDs.</summary>
    Task<IReadOnlyList<AppUser>> GetByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default);

    Task<AppUser> CreateAsync(AppUser user, CancellationToken ct = default);

    Task<AppUser?> UpdateAsync(Guid id, string displayName, bool isEnabled,
        List<string> roles, bool hasAllCatalogAccess, List<Guid> catalogIds,
        CancellationToken ct = default);

    Task<ServiceAccount?> UpdateServiceAccountAsync(Guid id, string name, string? description,
        bool isEnabled, List<string> roles, bool hasAllCatalogAccess, List<Guid> catalogIds,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the number of non-revoked API tokens that reference the given user as their
    /// <c>ActingUserId</c>. Used to guard against deleting a service account still in use.
    /// </summary>
    Task<int> GetActiveTokenCountByActingUserAsync(Guid userId, CancellationToken ct = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}
