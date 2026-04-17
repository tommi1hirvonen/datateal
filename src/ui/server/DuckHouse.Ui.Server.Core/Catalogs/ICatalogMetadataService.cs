namespace DuckHouse.Ui.Server.Core.Catalogs;

/// <summary>
/// Retrieves DuckLake catalog metadata (schemas, tables, views, columns)
/// via DuckDB with the DuckLake extension.
/// </summary>
public interface ICatalogMetadataService
{
    /// <summary>
    /// Queries the catalog's information_schema and returns the full object tree.
    /// </summary>
    Task<CatalogMetadataResult> GetMetadataAsync(
        string catalogName,
        string dataPath,
        string? storageConnectionString,
        string catalogHost,
        int catalogPort,
        string catalogDatabase,
        string catalogUser,
        string catalogPassword,
        CancellationToken cancellationToken = default);
}

public record CatalogMetadataResult(
    IReadOnlyList<CatalogSchemaResult> Schemas);

public record CatalogSchemaResult(
    string Name,
    IReadOnlyList<CatalogTableResult> Tables);

public record CatalogTableResult(
    string Name,
    string Type,
    IReadOnlyList<CatalogColumnResult> Columns);

public record CatalogColumnResult(
    string Name,
    string DataType,
    bool IsNullable,
    int OrdinalPosition);
