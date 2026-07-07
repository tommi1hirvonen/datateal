using Datateal.Core.Users;
using Datateal.Data;
using Datateal.Ui.Server.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Datateal.Ui.Server.Infrastructure.Data;

internal class UserRepository(DatatealDbContext db) : IUserRepository
{
    public async Task<IReadOnlyList<UserAccount>> GetAllUserAccountsAsync(CancellationToken ct = default) =>
        await db.UserAccounts
            .Include(u => u.CatalogAccessList)
                .ThenInclude(a => a.Catalog)
            .OrderBy(u => u.Email)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ServiceAccount>> GetAllServiceAccountsAsync(CancellationToken ct = default) =>
        await db.ServiceAccounts
            .Include(u => u.CatalogAccessList)
                .ThenInclude(a => a.Catalog)
            .OrderBy(u => u.Email)
            .ToListAsync(ct);

    public Task<AppUser?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.AppUsers
            .Include(u => u.CatalogAccessList)
                .ThenInclude(a => a.Catalog)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<AppUser?> GetByEmailAsync(string email, CancellationToken ct = default) =>
        db.AppUsers
            .Include(u => u.CatalogAccessList)
                .ThenInclude(a => a.Catalog)
            .FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<bool> EmailExistsAsync(string email, Guid? excludeId = null, CancellationToken ct = default)
    {
        var query = db.AppUsers.Where(u => u.Email == email);
        if (excludeId.HasValue)
            query = query.Where(u => u.Id != excludeId.Value);
        return query.AnyAsync(ct);
    }

    public async Task<IReadOnlyList<AppUser>> GetByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default) =>
        await db.AppUsers
            .AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .ToListAsync(ct);

    public async Task<AppUser> CreateAsync(AppUser user, CancellationToken ct = default)
    {
        db.AppUsers.Add(user);
        await db.SaveChangesAsync(ct);
        return user;
    }

    public async Task<AppUser?> UpdateAsync(Guid id, string displayName, bool isEnabled,
        List<string> roles, bool hasAllCatalogAccess, List<Guid> catalogIds,
        CancellationToken ct = default)
    {
        // Load entity without Include to avoid EF identity-cache issues when
        // AppClaimsTransformation has already tracked this entity in the same request.
        var existing = await db.AppUsers.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (existing is null) return null;

        existing.DisplayName = displayName;
        existing.IsEnabled = isEnabled;
        existing.Roles = roles;
        existing.HasAllCatalogAccess = hasAllCatalogAccess;
        existing.UpdatedAt = DateTime.UtcNow;

        await ReplaceCatalogAccessAsync(id, catalogIds, ct);

        // Populate CatalogAccessList on the entity for DTO mapping
        existing.CatalogAccessList = await db.UserCatalogAccess
            .Where(a => a.UserId == id)
            .Include(a => a.Catalog)
            .ToListAsync(ct);

        return existing;
    }

    public async Task<ServiceAccount?> UpdateServiceAccountAsync(Guid id, string name, string? description,
        bool isEnabled, List<string> roles, bool hasAllCatalogAccess, List<Guid> catalogIds,
        CancellationToken ct = default)
    {
        var existing = await db.ServiceAccounts.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (existing is null) return null;

        existing.Email = name;
        existing.DisplayName = name;
        existing.Description = description;
        existing.IsEnabled = isEnabled;
        existing.Roles = roles;
        existing.HasAllCatalogAccess = hasAllCatalogAccess;
        existing.UpdatedAt = DateTime.UtcNow;

        await ReplaceCatalogAccessAsync(id, catalogIds, ct);

        existing.CatalogAccessList = await db.UserCatalogAccess
            .Where(a => a.UserId == id)
            .Include(a => a.Catalog)
            .ToListAsync(ct);

        return existing;
    }

    public Task<int> GetActiveTokenCountByActingUserAsync(Guid userId, CancellationToken ct = default) =>
        db.ApiTokens
            .Where(t => t.ActingUserId == userId && !t.IsRevoked)
            .CountAsync(ct);

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var user = await db.AppUsers.FindAsync([id], ct);
        if (user is null) return false;
        db.AppUsers.Remove(user);
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Atomically replaces a user's catalog access list. Wrapped in an execution strategy
    /// because <see cref="Npgsql.EntityFrameworkCore.PostgreSQL"/> retrying strategy forbids
    /// user-initiated transactions outside a retriable unit.
    /// </summary>
    private async Task ReplaceCatalogAccessAsync(Guid userId, List<Guid> catalogIds, CancellationToken ct)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            await db.UserCatalogAccess
                .Where(a => a.UserId == userId)
                .ExecuteDeleteAsync(ct);

            foreach (var catalogId in catalogIds)
            {
                db.UserCatalogAccess.Add(new UserCatalogAccess
                {
                    Id = Guid.CreateVersion7(),
                    UserId = userId,
                    CatalogId = catalogId,
                });
            }

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
    }
}
