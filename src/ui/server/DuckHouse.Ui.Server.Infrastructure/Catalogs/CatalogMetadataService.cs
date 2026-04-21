using DuckHouse.Ui.Server.Core.Catalogs;
using Npgsql;

namespace DuckHouse.Ui.Server.Infrastructure.Catalogs;

internal class CatalogMetadataService : ICatalogMetadataService
{
    public async Task<CatalogMetadataResult> GetMetadataAsync(
        string catalogHost,
        int catalogPort,
        string catalogDatabase,
        string catalogUser,
        string catalogPassword,
        CancellationToken cancellationToken = default)
    {
        var connectionString = new NpgsqlConnectionStringBuilder
        {
            Host = catalogHost,
            Port = catalogPort,
            Username = catalogUser,
            Password = catalogPassword,
            Database = catalogDatabase,
        }.ConnectionString;

        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(cancellationToken);

        if (!await DuckLakeTablesExistAsync(conn, cancellationToken))
            return new CatalogMetadataResult([]);

        var snapshotId = await GetCurrentSnapshotAsync(conn, cancellationToken);
        if (snapshotId is null)
            return new CatalogMetadataResult([]);

        var schemas = await QuerySchemasAsync(conn, snapshotId.Value, cancellationToken);
        var tables = await QueryTablesAsync(conn, snapshotId.Value, cancellationToken);
        var views = await QueryViewsAsync(conn, snapshotId.Value, cancellationToken);
        var columns = await QueryColumnsAsync(conn, snapshotId.Value, cancellationToken);

        // Query comments — graceful fallback if tag tables are unavailable
        Dictionary<long, string> tableComments = [];
        Dictionary<long, string> viewComments = [];
        Dictionary<(long tableId, long columnId), string> columnComments = [];
        try
        {
            tableComments = await QueryObjectCommentsAsync(conn, snapshotId.Value, tables.Select(t => t.TableId), cancellationToken);
            viewComments = await QueryObjectCommentsAsync(conn, snapshotId.Value, views.Select(v => v.ViewId), cancellationToken);
            columnComments = await QueryColumnCommentsAsync(conn, snapshotId.Value, cancellationToken);
        }
        catch { /* tag tables may not exist in older DuckLake installations */ }

        var columnsByTableId = columns
            .GroupBy(c => c.TableId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<CatalogColumnResult>)g
                    .OrderBy(c => c.ColumnOrder)
                    .Select(c => new CatalogColumnResult(
                        c.ColumnName, c.ColumnType, c.NullsAllowed, (int)c.ColumnOrder,
                        columnComments.GetValueOrDefault((c.TableId, c.ColumnId))))
                    .ToList());

        var schemaResults = schemas
            .Select(schema =>
            {
                var schemaTables = tables
                    .Where(t => t.SchemaId == schema.SchemaId)
                    .Select(t => new CatalogTableResult(
                        t.TableName, "BASE TABLE",
                        columnsByTableId.GetValueOrDefault(t.TableId) ?? [],
                        tableComments.GetValueOrDefault(t.TableId)));

                var schemaViews = views
                    .Where(v => v.SchemaId == schema.SchemaId)
                    .Select(v => new CatalogTableResult(
                        v.ViewName, "VIEW", [],
                        viewComments.GetValueOrDefault(v.ViewId)));

                var allObjects = schemaTables.Concat(schemaViews)
                    .OrderBy(o => o.Name)
                    .ToList();

                return new CatalogSchemaResult(schema.SchemaName, allObjects);
            })
            .ToList();

        return new CatalogMetadataResult(schemaResults);
    }

    private static async Task<bool> DuckLakeTablesExistAsync(NpgsqlConnection conn, CancellationToken ct)
    {
        // Check all required DuckLake catalog tables are present.
        const string sql = """
            SELECT COUNT(*)
            FROM information_schema.tables
            WHERE table_schema = 'public'
              AND table_name IN (
                'ducklake_snapshot', 'ducklake_schema',
                'ducklake_table', 'ducklake_view', 'ducklake_column',
                'ducklake_tag', 'ducklake_column_tag'
              )
            """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        var count = (long)(await cmd.ExecuteScalarAsync(ct))!;
        return count == 7;
    }

    private static async Task<long?> GetCurrentSnapshotAsync(NpgsqlConnection conn, CancellationToken ct)
    {
        const string sql = """
            SELECT snapshot_id
            FROM ducklake_snapshot
            WHERE snapshot_id = (SELECT max(snapshot_id) FROM ducklake_snapshot)
            """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        var result = await cmd.ExecuteScalarAsync(ct);
        return result is DBNull or null ? null : (long)result;
    }

    private static async Task<IReadOnlyList<(long SchemaId, string SchemaName)>> QuerySchemasAsync(
        NpgsqlConnection conn, long snapshotId, CancellationToken ct)
    {
        var schemas = new List<(long, string)>();
        const string sql = """
            SELECT schema_id, schema_name
            FROM ducklake_schema
            WHERE @snapshotId >= begin_snapshot
              AND (@snapshotId < end_snapshot OR end_snapshot IS NULL)
            ORDER BY schema_name
            """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("snapshotId", snapshotId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            schemas.Add((reader.GetInt64(0), reader.GetString(1)));
        return schemas;
    }

    private static async Task<IReadOnlyList<(long SchemaId, long TableId, string TableName)>> QueryTablesAsync(
        NpgsqlConnection conn, long snapshotId, CancellationToken ct)
    {
        var tables = new List<(long, long, string)>();
        const string sql = """
            SELECT schema_id, table_id, table_name
            FROM ducklake_table
            WHERE @snapshotId >= begin_snapshot
              AND (@snapshotId < end_snapshot OR end_snapshot IS NULL)
            ORDER BY schema_id, table_name
            """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("snapshotId", snapshotId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            tables.Add((reader.GetInt64(0), reader.GetInt64(1), reader.GetString(2)));
        return tables;
    }

    private static async Task<IReadOnlyList<(long SchemaId, long ViewId, string ViewName)>> QueryViewsAsync(
        NpgsqlConnection conn, long snapshotId, CancellationToken ct)
    {
        var views = new List<(long, long, string)>();
        const string sql = """
            SELECT schema_id, view_id, view_name
            FROM ducklake_view
            WHERE @snapshotId >= begin_snapshot
              AND (@snapshotId < end_snapshot OR end_snapshot IS NULL)
            ORDER BY schema_id, view_name
            """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("snapshotId", snapshotId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            views.Add((reader.GetInt64(0), reader.GetInt64(1), reader.GetString(2)));
        return views;
    }

    private static async Task<IReadOnlyList<(long TableId, long ColumnId, string ColumnName, string ColumnType, bool NullsAllowed, long ColumnOrder)>> QueryColumnsAsync(
        NpgsqlConnection conn, long snapshotId, CancellationToken ct)
    {
        var columns = new List<(long, long, string, string, bool, long)>();
        const string sql = """
            SELECT table_id, column_id, column_name, column_type, nulls_allowed, column_order
            FROM ducklake_column
            WHERE parent_column IS NULL
              AND @snapshotId >= begin_snapshot
              AND (@snapshotId < end_snapshot OR end_snapshot IS NULL)
            ORDER BY table_id, column_order
            """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("snapshotId", snapshotId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            columns.Add((
                reader.GetInt64(0),   // table_id
                reader.GetInt64(1),   // column_id
                reader.GetString(2),  // column_name
                reader.GetString(3),  // column_type
                reader.GetBoolean(4), // nulls_allowed
                reader.GetInt64(5)    // column_order
            ));
        return columns;
    }

    private static async Task<Dictionary<long, string>> QueryObjectCommentsAsync(
        NpgsqlConnection conn, long snapshotId, IEnumerable<long> objectIds, CancellationToken ct)
    {
        var ids = objectIds.ToList();
        if (ids.Count == 0) return [];

        var comments = new Dictionary<long, string>();
        const string sql = """
            SELECT object_id, value
            FROM ducklake_tag
            WHERE key = 'comment'
              AND object_id = ANY(@ids)
              AND @snapshotId >= begin_snapshot
              AND (@snapshotId < end_snapshot OR end_snapshot IS NULL)
            """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("snapshotId", snapshotId);
        cmd.Parameters.AddWithValue("ids", ids.ToArray());
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            comments[reader.GetInt64(0)] = reader.GetString(1);
        return comments;
    }

    private static async Task<Dictionary<(long tableId, long columnId), string>> QueryColumnCommentsAsync(
        NpgsqlConnection conn, long snapshotId, CancellationToken ct)
    {
        var comments = new Dictionary<(long, long), string>();
        const string sql = """
            SELECT table_id, column_id, value
            FROM ducklake_column_tag
            WHERE key = 'comment'
              AND @snapshotId >= begin_snapshot
              AND (@snapshotId < end_snapshot OR end_snapshot IS NULL)
            """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("snapshotId", snapshotId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            comments[(reader.GetInt64(0), reader.GetInt64(1))] = reader.GetString(2);
        return comments;
    }
}
