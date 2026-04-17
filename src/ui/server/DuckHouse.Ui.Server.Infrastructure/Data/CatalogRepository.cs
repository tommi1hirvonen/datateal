using DuckHouse.Core.Catalogs;
using DuckHouse.Data;
using DuckHouse.Ui.Server.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DuckHouse.Ui.Server.Infrastructure.Data;

internal class CatalogRepository(DuckHouseDbContext db) : ICatalogRepository
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
        existing.IsManaged = catalog.IsManaged;
        existing.DataPath = catalog.DataPath;
        existing.EncryptedStorageConnectionString = catalog.EncryptedStorageConnectionString;
        existing.CatalogHost = catalog.CatalogHost;
        existing.CatalogPort = catalog.CatalogPort;
        existing.CatalogDatabase = catalog.CatalogDatabase;
        existing.CatalogUser = catalog.CatalogUser;
        existing.EncryptedCatalogPassword = catalog.EncryptedCatalogPassword;
        existing.UpdatedAt = DateTime.UtcNow;

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
}
