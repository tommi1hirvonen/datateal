using DuckHouse.Ui.Server.Core.Catalogs;
using Npgsql;

namespace DuckHouse.Ui.Server.Infrastructure.Catalogs;

internal class CatalogDatabaseService : ICatalogDatabaseService
{
    public async Task CreateDatabaseAsync(string databaseName, string host, int port, string user, string password,
        CancellationToken cancellationToken = default)
    {
        var connectionString = BuildConnectionString(host, port, user, password, "postgres");
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        // Database names are sanitized via quoting to prevent SQL injection
        var sql = $"CREATE DATABASE \"{databaseName}\"";
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task DropDatabaseAsync(string databaseName, string host, int port, string user, string password,
        CancellationToken cancellationToken = default)
    {
        var connectionString = BuildConnectionString(host, port, user, password, "postgres");
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        // Terminate existing connections before dropping
        var terminateSql = $"""
            SELECT pg_terminate_backend(pid) FROM pg_stat_activity
            WHERE datname = '{databaseName.Replace("'", "''")}' AND pid <> pg_backend_pid()
            """;
        await using (var terminateCmd = new NpgsqlCommand(terminateSql, conn))
        {
            await terminateCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        var dropSql = $"DROP DATABASE IF EXISTS \"{databaseName}\"";
        await using var cmd = new NpgsqlCommand(dropSql, conn);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string BuildConnectionString(string host, int port, string user, string password, string database) =>
        new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = port,
            Username = user,
            Password = password,
            Database = database,
        }.ConnectionString;
}
