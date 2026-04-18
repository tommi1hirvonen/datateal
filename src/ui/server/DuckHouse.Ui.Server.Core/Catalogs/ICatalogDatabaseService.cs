namespace DuckHouse.Ui.Server.Core.Catalogs;

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
}
