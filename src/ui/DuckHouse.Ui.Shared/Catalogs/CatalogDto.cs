namespace DuckHouse.Ui.Shared.Catalogs;

public record CatalogDto(
    Guid Id,
    string Name,
    bool IsManaged,
    string? DataPath,
    string? CatalogHost,
    int? CatalogPort,
    string? CatalogDatabase,
    string? CatalogUser,
    bool HasStorageConnectionString,
    bool HasCatalogPassword,
    DateTime CreatedAt,
    DateTime UpdatedAt);
