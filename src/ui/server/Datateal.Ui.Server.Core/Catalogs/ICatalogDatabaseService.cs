namespace Datateal.Ui.Server.Core.Catalogs;

/// <summary>
/// Manages PostgreSQL databases for DuckLake catalog metadata.
/// </summary>
public interface ICatalogDatabaseService
{
    /// <summary>
    /// Creates a new PostgreSQL database with the given name.
    /// Returns <c>true</c> if the database was newly created, or <c>false</c> if it already existed
    /// and <paramref name="allowExistingDatabase"/> was <c>true</c>.
    /// </summary>
    Task<bool> CreateDatabaseAsync(string databaseName, string host, int port, string user, string password,
        bool allowExistingDatabase = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Drops the PostgreSQL database with the given name.
    /// </summary>
    Task DropDatabaseAsync(string databaseName, string host, int port, string user, string password,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads DuckLake catalog settings from the <c>ducklake_metadata</c> table via Npgsql.
    /// Returns <c>null</c> if the table does not exist or the connection fails.
    /// </summary>
    Task<(bool ParquetV2, bool PerThreadOutput)?> GetDuckLakeSettingsAsync(
        string host, int port, string database, string user, string password,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes DuckLake catalog settings using DuckDB with the DuckLake extension.
    /// Attaches the catalog (initializing its schema on first call), calls <c>set_option()</c>,
    /// then detaches. The first ATTACH on a new database creates the DuckLake schema correctly.
    /// </summary>
    Task SetDuckLakeSettingsAsync(
        string host, int port, string database, string user, string password,
        string dataPath, string? storageConnectionString, string catalogName,
        bool parquetV2, bool perThreadOutput,
        CancellationToken cancellationToken = default);
}
