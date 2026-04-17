namespace DuckHouse.Ui.Server.Core.Catalogs;

/// <summary>
/// Manages PostgreSQL databases for DuckLake catalog metadata.
/// </summary>
public interface ICatalogDatabaseService
{
    /// <summary>
    /// Creates a new PostgreSQL database with the given name.
    /// </summary>
    Task CreateDatabaseAsync(string databaseName, string host, int port, string user, string password,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Drops the PostgreSQL database with the given name.
    /// </summary>
    Task DropDatabaseAsync(string databaseName, string host, int port, string user, string password,
        CancellationToken cancellationToken = default);
}
