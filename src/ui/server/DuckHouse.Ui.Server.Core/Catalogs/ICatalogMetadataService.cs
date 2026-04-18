namespace DuckHouse.Ui.Server.Core.Catalogs;

/// <summary>
/// Retrieves DuckLake catalog metadata (schemas, tables, views, columns)
/// by querying the DuckLake catalog tables directly in Postgres.
/// </summary>
public interface ICatalogMetadataService
{
    Task<CatalogMetadataResult> GetMetadataAsync(
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
