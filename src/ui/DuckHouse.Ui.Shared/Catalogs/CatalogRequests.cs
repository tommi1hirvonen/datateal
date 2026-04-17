namespace DuckHouse.Ui.Shared.Catalogs;

public record CreateCatalogRequest(
    string Name,
    bool IsManaged,
    string? DataPath = null,
    string? StorageConnectionString = null,
    string? CatalogHost = null,
    int? CatalogPort = null,
    string? CatalogDatabase = null,
    string? CatalogUser = null,
    string? CatalogPassword = null);

public record UpdateCatalogRequest(
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
