using DuckHouse.Core.Users;
using DuckHouse.Data;
using DuckHouse.Ui.Server.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DuckHouse.Ui.Server.Infrastructure.Data;

internal class UserRepository(DuckHouseDbContext db) : IUserRepository
{
    public async Task<IReadOnlyList<AppUser>> GetAllAsync(CancellationToken ct = default) =>
        await db.AppUsers
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

        // Replace catalog access atomically.  ExecuteDeleteAsync bypasses the change
        // tracker so there is no EF identity conflict with previously-loaded entries.
        // Wrap in CreateExecutionStrategy().ExecuteAsync() because
        // NpgsqlRetryingExecutionStrategy does not allow user-initiated transactions
        // outside of a retriable unit.
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            await db.UserCatalogAccess
                .Where(a => a.UserId == id)
                .ExecuteDeleteAsync(ct);

            foreach (var catalogId in catalogIds)
            {
                db.UserCatalogAccess.Add(new UserCatalogAccess
                {
                    Id = Guid.CreateVersion7(),
                    UserId = id,
                    CatalogId = catalogId,
                });
            }

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });

        // Populate CatalogAccessList on the entity for DTO mapping
        existing.CatalogAccessList = await db.UserCatalogAccess
            .Where(a => a.UserId == id)
            .Include(a => a.Catalog)
            .ToListAsync(ct);

        return existing;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var user = await db.AppUsers.FindAsync([id], ct);
        if (user is null) return false;
        db.AppUsers.Remove(user);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
