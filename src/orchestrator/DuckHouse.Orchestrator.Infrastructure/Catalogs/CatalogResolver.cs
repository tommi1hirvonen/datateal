using DuckHouse.Core.Catalogs;
using DuckHouse.Data;
using DuckHouse.Orchestrator.Core.Configuration;
using DuckHouse.Orchestrator.Core.Interfaces;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DuckHouse.Orchestrator.Infrastructure.Catalogs;

internal class CatalogResolver(
    DuckHouseDbContext db,
    IDataProtectionProvider dataProtectionProvider,
    IOptions<CatalogSettings> settingsOptions) : ICatalogResolver
{
    private readonly IDataProtector _protector =
        dataProtectionProvider.CreateProtector("DuckHouse.Catalogs");
    private readonly CatalogSettings _settings = settingsOptions.Value;

    public async Task<IReadOnlyList<ResolvedCatalog>> ResolveAsync(
        IReadOnlyList<string> catalogNames, CancellationToken ct)
    {
        if (catalogNames.Count == 0) return [];

        var catalogs = await db.Catalogs
            .Where(c => catalogNames.Contains(c.Name))
            .ToListAsync(ct);

        var results = new List<ResolvedCatalog>(catalogs.Count);

        foreach (var catalog in catalogs)
        {
            string dataPath;
            string? storageConnectionString;
            string catalogHost;
            int catalogPort;
            string catalogDatabase;
            string catalogUser;
            string catalogPassword;

            if (catalog is ManagedCatalog)
            {
                var basePath = _settings.BaseDataPath.TrimEnd('/');
                dataPath = $"{basePath}/{catalog.Name}";
                storageConnectionString = _settings.StorageConnectionString;
                catalogHost = _settings.CatalogPodHost ?? _settings.CatalogHost;
                catalogPort = _settings.CatalogPort;
                catalogDatabase = catalog.Name;
                catalogUser = _settings.CatalogUser;
                catalogPassword = _settings.CatalogPassword;
            }
            else
            {
                var u = (UnmanagedCatalog)catalog;
                dataPath = u.DataPath;
                storageConnectionString = u.EncryptedStorageConnectionString is not null
                    ? _protector.Unprotect(u.EncryptedStorageConnectionString)
                    : null;
                catalogHost = u.CatalogHost;
                catalogPort = u.CatalogPort;
                catalogDatabase = u.CatalogDatabase;
                catalogUser = u.CatalogUser;
                catalogPassword = u.EncryptedCatalogPassword is not null
                    ? _protector.Unprotect(u.EncryptedCatalogPassword)
                    : string.Empty;
            }

            results.Add(new ResolvedCatalog(
                catalog.Name, dataPath, storageConnectionString,
                catalogHost, catalogPort, catalogDatabase,
                catalogUser, catalogPassword));
        }

        return results;
    }
}
