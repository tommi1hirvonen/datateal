namespace DuckHouse.Ui.Shared.Catalogs;

public record CreateManagedCatalogRequest(string Name);

public record CreateUnmanagedCatalogRequest(
    string Name,
    string DataPath,
    string? StorageConnectionString = null,
    string CatalogHost = "localhost",
    int CatalogPort = 5432,
    string CatalogDatabase = "",
    string CatalogUser = "",
    string? CatalogPassword = null);

public record UpdateManagedCatalogRequest(string Name);

public record UpdateUnmanagedCatalogRequest(
    string Name,
    string? DataPath = null,
    string? StorageConnectionString = null,
    string? CatalogHost = null,
    int? CatalogPort = null,
    string? CatalogDatabase = null,
    string? CatalogUser = null,
    string? CatalogPassword = null);

public record UpdateWorkspaceItemCatalogsRequest(List<string> CatalogNames);

public record ResolveCatalogsRequest(List<string> CatalogNames);
