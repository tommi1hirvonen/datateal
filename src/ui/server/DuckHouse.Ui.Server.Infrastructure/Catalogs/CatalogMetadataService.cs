using DuckDB.NET.Data;
using DuckHouse.Ui.Server.Core.Catalogs;

namespace DuckHouse.Ui.Server.Infrastructure.Catalogs;

internal class CatalogMetadataService : ICatalogMetadataService
{
    public async Task<CatalogMetadataResult> GetMetadataAsync(
        string catalogName,
        string dataPath,
        string? storageConnectionString,
        string catalogHost,
        int catalogPort,
        string catalogDatabase,
        string catalogUser,
        string catalogPassword,
        CancellationToken cancellationToken = default)
    {
        await using var connection = new DuckDBConnection("DataSource=:memory:");
        await connection.OpenAsync(cancellationToken);

        await ExecuteNonQueryAsync(connection, "INSTALL ducklake", cancellationToken);
        await ExecuteNonQueryAsync(connection, "LOAD ducklake", cancellationToken);

        var usesAzure = storageConnectionString is not null;
        if (usesAzure)
        {
            await ExecuteNonQueryAsync(connection, "INSTALL azure", cancellationToken);
            await ExecuteNonQueryAsync(connection, "LOAD azure", cancellationToken);

            if (OperatingSystem.IsLinux())
                await ExecuteNonQueryAsync(connection, "SET azure_transport_option_type = 'curl'", cancellationToken);

            await ExecuteNonQueryAsync(connection,
                $"""
                CREATE SECRET __metadata_azure (
                    TYPE azure,
                    CONNECTION_STRING '{EscapeSql(storageConnectionString!)}'
                )
                """,
                cancellationToken);
        }

        await ExecuteNonQueryAsync(connection,
            $"""
            CREATE SECRET __metadata_pg (
                TYPE postgres,
                HOST '{EscapeSql(catalogHost)}',
                PORT {catalogPort},
                DATABASE '{EscapeSql(catalogDatabase)}',
                USER '{EscapeSql(catalogUser)}',
                PASSWORD '{EscapeSql(catalogPassword)}'
            )
            """,
            cancellationToken);

        await ExecuteNonQueryAsync(connection,
            $"ATTACH 'ducklake:postgres:' AS {catalogName} (DATA_PATH '{EscapeSql(dataPath)}')",
            cancellationToken);

        var schemas = await QuerySchemasAsync(connection, catalogName, cancellationToken);
        return new CatalogMetadataResult(schemas);
    }

    private static async Task<IReadOnlyList<CatalogSchemaResult>> QuerySchemasAsync(
        DuckDBConnection connection, string catalogName, CancellationToken ct)
    {
        var schemas = new List<CatalogSchemaResult>();

        // Get all schemas
        var schemaNames = new List<string>();
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = $"""
                SELECT schema_name FROM {catalogName}.information_schema.schemata
                WHERE schema_name NOT IN ('information_schema', 'pg_catalog')
                ORDER BY schema_name
                """;
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                schemaNames.Add(reader.GetString(0));
        }

        foreach (var schemaName in schemaNames)
        {
            var tables = await QueryTablesAsync(connection, catalogName, schemaName, ct);
            schemas.Add(new CatalogSchemaResult(schemaName, tables));
        }

        return schemas;
    }

    private static async Task<IReadOnlyList<CatalogTableResult>> QueryTablesAsync(
        DuckDBConnection connection, string catalogName, string schemaName, CancellationToken ct)
    {
        var tables = new List<(string Name, string Type)>();

        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = $"""
                SELECT table_name, table_type FROM {catalogName}.information_schema.tables
                WHERE table_schema = '{EscapeSql(schemaName)}'
                ORDER BY table_name
                """;
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                tables.Add((reader.GetString(0), reader.GetString(1)));
        }

        var result = new List<CatalogTableResult>();
        foreach (var (tableName, tableType) in tables)
        {
            var columns = await QueryColumnsAsync(connection, catalogName, schemaName, tableName, ct);
            result.Add(new CatalogTableResult(tableName, tableType, columns));
        }

        return result;
    }

    private static async Task<IReadOnlyList<CatalogColumnResult>> QueryColumnsAsync(
        DuckDBConnection connection, string catalogName, string schemaName, string tableName, CancellationToken ct)
    {
        var columns = new List<CatalogColumnResult>();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"""
            SELECT column_name, data_type, is_nullable, ordinal_position
            FROM {catalogName}.information_schema.columns
            WHERE table_schema = '{EscapeSql(schemaName)}' AND table_name = '{EscapeSql(tableName)}'
            ORDER BY ordinal_position
            """;
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            columns.Add(new CatalogColumnResult(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2) == "YES",
                reader.GetInt32(3)));
        }

        return columns;
    }

    private static async Task ExecuteNonQueryAsync(DuckDBConnection connection, string sql, CancellationToken ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static string EscapeSql(string value) => value.Replace("'", "''");
}
