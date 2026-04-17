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

            if (catalog.IsManaged)
            {
                var basePath = _settings.BaseDataPath.TrimEnd('/');
                dataPath = $"{basePath}/{catalog.Name}";
                storageConnectionString = _settings.StorageConnectionString;
                catalogHost = _settings.CatalogHost;
                catalogPort = _settings.CatalogPort;
                catalogDatabase = catalog.Name;
                catalogUser = _settings.CatalogUser;
                catalogPassword = _settings.CatalogPassword;
            }
            else
            {
                dataPath = catalog.DataPath!;
                storageConnectionString = catalog.EncryptedStorageConnectionString is not null
                    ? _protector.Unprotect(catalog.EncryptedStorageConnectionString)
                    : null;
                catalogHost = catalog.CatalogHost!;
                catalogPort = catalog.CatalogPort!.Value;
                catalogDatabase = catalog.CatalogDatabase!;
                catalogUser = catalog.CatalogUser!;
                catalogPassword = _protector.Unprotect(catalog.EncryptedCatalogPassword!);
            }

            results.Add(new ResolvedCatalog(
                catalog.Name, dataPath, storageConnectionString,
                catalogHost, catalogPort, catalogDatabase,
                catalogUser, catalogPassword));
        }

        return results;
    }
}
