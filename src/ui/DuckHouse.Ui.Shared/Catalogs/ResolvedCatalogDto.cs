namespace DuckHouse.Ui.Shared.Catalogs;

/// <summary>
/// Contains fully resolved (decrypted) catalog connection details.
/// Only returned by the resolve endpoint — never persisted on the client.
/// </summary>
public record ResolvedCatalogDto(
    string Name,
    string DataPath,
    string? StorageConnectionString,
    string CatalogHost,
    int CatalogPort,
    string CatalogDatabase,
    string CatalogUser,
    string CatalogPassword);
