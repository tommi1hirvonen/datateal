using Datateal.Core.Catalogs;
using Datateal.Data;
using Datateal.Ui.Server.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Datateal.Ui.Server.Infrastructure.Data;

internal class CatalogRepository(DatatealDbContext db) : ICatalogRepository
{
    public async Task<IReadOnlyList<Catalog>> GetAllAsync(CancellationToken ct = default) =>
        await db.Catalogs.OrderBy(c => c.Name).ToListAsync(ct);

    public Task<Catalog?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Catalogs.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<Catalog?> GetByNameAsync(string name, CancellationToken ct = default) =>
        db.Catalogs.FirstOrDefaultAsync(c => c.Name == name, ct);

    public Task<bool> CatalogNameExistsAsync(string name, Guid? excludeId = null, CancellationToken ct = default)
    {
        var query = db.Catalogs.Where(c => c.Name == name);
        if (excludeId.HasValue)
            query = query.Where(c => c.Id != excludeId.Value);
        return query.AnyAsync(ct);
    }

    public async Task<Catalog> CreateAsync(Catalog catalog, CancellationToken ct = default)
    {
        db.Catalogs.Add(catalog);
        await db.SaveChangesAsync(ct);
        return catalog;
    }

    public async Task<Catalog?> UpdateAsync(Catalog catalog, CancellationToken ct = default)
    {
        var existing = await db.Catalogs.FirstOrDefaultAsync(c => c.Id == catalog.Id, ct);
        if (existing is null) return null;

        existing.Name = catalog.Name;
        existing.UpdatedAt = DateTime.UtcNow;

        if (existing is UnmanagedCatalog existingExternal && catalog is UnmanagedCatalog updateExternal)
        {
            existingExternal.DataPath = updateExternal.DataPath;
            existingExternal.EncryptedStorageConnectionString = updateExternal.EncryptedStorageConnectionString;
            existingExternal.CatalogHost = updateExternal.CatalogHost;
            existingExternal.CatalogPort = updateExternal.CatalogPort;
            existingExternal.CatalogDatabase = updateExternal.CatalogDatabase;
            existingExternal.CatalogUser = updateExternal.CatalogUser;
            existingExternal.EncryptedCatalogPassword = updateExternal.EncryptedCatalogPassword;
        }

        await db.SaveChangesAsync(ct);
        return existing;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var catalog = await db.Catalogs.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (catalog is null) return false;

        db.Catalogs.Remove(catalog);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<Catalog>> GetByNamesAsync(IReadOnlyList<string> names, CancellationToken ct = default) =>
        await db.Catalogs.Where(c => names.Contains(c.Name)).ToListAsync(ct);

    public async Task<IReadOnlyList<string>> GetWorkspaceAccessibleNamesAsync(Guid workspaceId, IReadOnlyList<string> names, CancellationToken ct = default) =>
        await db.Catalogs
            .Where(c => names.Contains(c.Name)
                && (c.AccessibleFromAllWorkspaces
                    || db.CatalogWorkspaceAccess.Any(a => a.CatalogId == c.Id && a.WorkspaceId == workspaceId)))
            .Select(c => c.Name)
            .ToListAsync(ct);

    public async Task<(bool AccessibleFromAllWorkspaces, IReadOnlyList<Guid> WorkspaceIds)?> GetWorkspaceAccessAsync(Guid catalogId, CancellationToken ct = default)
    {
        var catalog = await db.Catalogs
            .Where(c => c.Id == catalogId)
            .Select(c => new { c.AccessibleFromAllWorkspaces })
            .FirstOrDefaultAsync(ct);
        if (catalog is null) return null;

        var workspaceIds = await db.CatalogWorkspaceAccess
            .Where(a => a.CatalogId == catalogId)
            .Select(a => a.WorkspaceId)
            .ToListAsync(ct);

        return (catalog.AccessibleFromAllWorkspaces, workspaceIds);
    }

    public async Task<bool> SetWorkspaceAccessAsync(Guid catalogId, bool accessibleFromAllWorkspaces, IReadOnlyList<Guid> workspaceIds, CancellationToken ct = default)
    {
        var catalog = await db.Catalogs.FirstOrDefaultAsync(c => c.Id == catalogId, ct);
        if (catalog is null) return false;

        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            await db.CatalogWorkspaceAccess
                .Where(a => a.CatalogId == catalogId)
                .ExecuteDeleteAsync(ct);

            catalog.AccessibleFromAllWorkspaces = accessibleFromAllWorkspaces;
            catalog.UpdatedAt = DateTime.UtcNow;

            if (!accessibleFromAllWorkspaces)
            {
                foreach (var workspaceId in workspaceIds.Distinct())
                {
                    db.CatalogWorkspaceAccess.Add(new CatalogWorkspaceAccess
                    {
                        Id = Guid.CreateVersion7(),
                        CatalogId = catalogId,
                        WorkspaceId = workspaceId,
                    });
                }
            }

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });

        return true;
    }
}
