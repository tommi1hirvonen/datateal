using DuckDB.NET.Data;
using Datateal.Ui.Server.Core.Catalogs;
using Npgsql;

namespace Datateal.Ui.Server.Infrastructure.Catalogs;

internal class CatalogDatabaseService : ICatalogDatabaseService
{
    public async Task<bool> CreateDatabaseAsync(string databaseName, string host, int port, string user, string password,
        bool allowExistingDatabase = false, CancellationToken cancellationToken = default)
    {
        var connectionString = BuildConnectionString(host, port, user, password, "postgres");
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        if (allowExistingDatabase)
        {
            var checkSql = "SELECT 1 FROM pg_database WHERE datname = @name";
            await using var checkCmd = new NpgsqlCommand(checkSql, conn);
            checkCmd.Parameters.AddWithValue("name", databaseName);
            var exists = await checkCmd.ExecuteScalarAsync(cancellationToken) is not null;
            if (exists)
                return false;
        }

        // Database names are sanitized via quoting to prevent SQL injection
        var sql = $"CREATE DATABASE \"{databaseName}\"";
        await using var cmd = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
        return true;
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

    public async Task<(bool ParquetV2, bool PerThreadOutput)?> GetDuckLakeSettingsAsync(
        string host, int port, string database, string user, string password,
        CancellationToken cancellationToken = default)
    {
        var connectionString = BuildConnectionString(host, port, user, password, database);
        try
        {
            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync(cancellationToken);

            const string sql = """
                SELECT key, value FROM ducklake_metadata
                WHERE key IN ('parquet_version', 'per_thread_output') AND scope IS NULL
                """;
            await using var cmd = new NpgsqlCommand(sql, conn);
            var rows = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                rows[reader.GetString(0)] = reader.GetString(1);

            var parquetV2 = rows.TryGetValue("parquet_version", out var pv) && pv.Equals("V2", StringComparison.OrdinalIgnoreCase);
            var perThreadOutput = rows.TryGetValue("per_thread_output", out var pto) && pto.Equals("true", StringComparison.OrdinalIgnoreCase);
            return (parquetV2, perThreadOutput);
        }
        catch
        {
            return null;
        }
    }

    public Task SetDuckLakeSettingsAsync(
        string host, int port, string database, string user, string password,
        string dataPath, string? storageConnectionString, string catalogName,
        bool parquetV2, bool perThreadOutput,
        CancellationToken cancellationToken = default)
    {
        // DuckDB operations are synchronous; run on a thread pool thread to avoid blocking.
        return Task.Run(() => SetDuckLakeSettingsCore(
            host, port, database, user, password, dataPath, storageConnectionString, catalogName, parquetV2, perThreadOutput),
            cancellationToken);
    }

    private static void SetDuckLakeSettingsCore(
        string host, int port, string database, string user, string password,
        string dataPath, string? storageConnectionString, string catalogName,
        bool parquetV2, bool perThreadOutput)
    {
        using var conn = new DuckDBConnection("DataSource=:memory:");
        conn.Open();
        using var cmd = conn.CreateCommand();

        // Load DuckLake extension
        cmd.CommandText = "INSTALL ducklake; LOAD ducklake;";
        cmd.ExecuteNonQuery();

        // Create a unique secret suffix per call to avoid collision with concurrent calls
        var suffix = Guid.NewGuid().ToString("N")[..8];

        if (storageConnectionString is not null)
        {
            cmd.CommandText = $"""
                CREATE SECRET secret_{suffix}_storage (
                  TYPE azure,
                  CONNECTION_STRING '{EscapeSql(storageConnectionString)}',
                  SCOPE '{GetAzureScope(dataPath)}'
                )
                """;
            cmd.ExecuteNonQuery();
        }

        cmd.CommandText = $"""
            CREATE SECRET secret_{suffix}_pg (
              TYPE postgres,
              HOST '{EscapeSql(host)}',
              PORT {port},
              DATABASE '{EscapeSql(database)}',
              USER '{EscapeSql(user)}',
              PASSWORD '{EscapeSql(password)}',
              SCOPE 'postgres://{EscapeSql(host)}:{port}/{EscapeSql(database)}'
            )
            """;
        cmd.ExecuteNonQuery();

        cmd.CommandText = $"""
            ATTACH 'ducklake:postgres:' AS "{catalogName}"
            (DATA_PATH '{EscapeSql(dataPath)}', META_SECRET 'secret_{suffix}_pg', AUTOMATIC_MIGRATION true)
            """;
        cmd.ExecuteNonQuery();

        // Apply settings using DuckLake's set_option() function
        cmd.CommandText = $"CALL \"{catalogName}\".set_option('parquet_version', '{(parquetV2 ? "2" : "1")}')";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $"CALL \"{catalogName}\".set_option('per_thread_output', '{(perThreadOutput ? "true" : "false")}')";
        cmd.ExecuteNonQuery();

        cmd.CommandText = $"DETACH \"{catalogName}\"";
        cmd.ExecuteNonQuery();
    }

    private static string EscapeSql(string value) => value.Replace("'", "''");

    private static string GetAzureScope(string dataPath)
    {
        if (dataPath.StartsWith("abfss://", StringComparison.OrdinalIgnoreCase) ||
            dataPath.StartsWith("az://", StringComparison.OrdinalIgnoreCase))
            return dataPath;
        return "az://";
    }
}
