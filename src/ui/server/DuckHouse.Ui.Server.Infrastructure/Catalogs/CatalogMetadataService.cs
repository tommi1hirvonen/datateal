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
            CREATE SECRET (
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

        // Bulk-fetch all metadata in 4 queries then assemble the tree in memory.
        var schemaNames = await QuerySchemasAsync(connection, catalogName, cancellationToken);
        var tables = await QueryTablesAsync(connection, catalogName, cancellationToken);
        var views = await QueryViewsAsync(connection, catalogName, cancellationToken);
        var columns = await QueryColumnsAsync(connection, catalogName, cancellationToken);

        var columnsByTable = columns
            .GroupBy(c => (c.SchemaName, c.TableName))
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<CatalogColumnResult>)g
                    .OrderBy(c => c.ColumnIndex)
                    .Select(c => new CatalogColumnResult(c.ColumnName, c.DataType, c.IsNullable, c.ColumnIndex))
                    .ToList());

        var schemaResults = schemaNames
            .Select(schemaName =>
            {
                var schemaTables = tables
                    .Where(t => t.SchemaName == schemaName)
                    .Select(t => new CatalogTableResult(
                        t.TableName, "BASE TABLE",
                        columnsByTable.GetValueOrDefault((schemaName, t.TableName)) ?? []));

                var schemaViews = views
                    .Where(v => v.SchemaName == schemaName)
                    .Select(v => new CatalogTableResult(
                        v.ViewName, "VIEW",
                        columnsByTable.GetValueOrDefault((schemaName, v.ViewName)) ?? []));

                var allObjects = schemaTables.Concat(schemaViews)
                    .OrderBy(o => o.Name)
                    .ToList();

                return new CatalogSchemaResult(schemaName, allObjects);
            })
            .ToList();

        return new CatalogMetadataResult(schemaResults);
    }

    private static async Task<IReadOnlyList<string>> QuerySchemasAsync(
        DuckDBConnection connection, string catalogName, CancellationToken ct)
    {
        var schemas = new List<string>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"""
            SELECT schema_name
            FROM duckdb_schemas()
            WHERE database_name = '{EscapeSql(catalogName)}' AND NOT internal
            ORDER BY schema_name
            """;
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            schemas.Add(reader.GetString(0));
        return schemas;
    }

    private static async Task<IReadOnlyList<(string SchemaName, string TableName)>> QueryTablesAsync(
        DuckDBConnection connection, string catalogName, CancellationToken ct)
    {
        var tables = new List<(string, string)>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"""
            SELECT schema_name, table_name
            FROM duckdb_tables()
            WHERE database_name = '{EscapeSql(catalogName)}' AND NOT internal
            ORDER BY schema_name, table_name
            """;
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            tables.Add((reader.GetString(0), reader.GetString(1)));
        return tables;
    }

    private static async Task<IReadOnlyList<(string SchemaName, string ViewName)>> QueryViewsAsync(
        DuckDBConnection connection, string catalogName, CancellationToken ct)
    {
        var views = new List<(string, string)>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"""
            SELECT schema_name, view_name
            FROM duckdb_views()
            WHERE database_name = '{EscapeSql(catalogName)}' AND NOT internal
            ORDER BY schema_name, view_name
            """;
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            views.Add((reader.GetString(0), reader.GetString(1)));
        return views;
    }

    private static async Task<IReadOnlyList<(string SchemaName, string TableName, string ColumnName, string DataType, bool IsNullable, int ColumnIndex)>> QueryColumnsAsync(
        DuckDBConnection connection, string catalogName, CancellationToken ct)
    {
        var columns = new List<(string, string, string, string, bool, int)>();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"""
            SELECT schema_name, table_name, column_name, data_type, is_nullable, column_index
            FROM duckdb_columns()
            WHERE database_name = '{EscapeSql(catalogName)}' AND NOT internal
            ORDER BY schema_name, table_name, column_index
            """;
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            columns.Add((
                reader.GetString(0),   // schema_name
                reader.GetString(1),   // table_name
                reader.GetString(2),   // column_name
                reader.GetString(3),   // data_type
                reader.GetBoolean(4),  // is_nullable (boolean in duckdb_columns)
                reader.GetInt32(5)     // column_index (0-based)
            ));
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
