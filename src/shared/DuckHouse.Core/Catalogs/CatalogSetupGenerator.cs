using System.Text;

namespace DuckHouse.Core.Catalogs;

/// <summary>
/// Generates DuckDB setup commands for attaching DuckLake catalogs to a kernel session.
/// </summary>
public static class CatalogSetupGenerator
{
    /// <summary>
    /// Generates the complete setup script for the given resolved catalogs as Python code
    /// that runs DuckDB commands via <c>duckdb.execute()</c>, matching the kernel's Python environment.
    /// </summary>
    public static string GenerateSetupScript(IReadOnlyList<ResolvedCatalog> catalogs)
    {
        if (catalogs.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        AppendPreamble(sb);

        if (catalogs.Any(c => c.StorageConnectionString is not null))
            AppendAzureSetup(sb);

        foreach (var catalog in catalogs)
        {
            var suffix = catalogs.Count > 1 ? $"_{catalog.Name}" : "";
            AppendCatalogSecrets(sb, catalog, suffix, createOrReplace: false);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generates a Python script to attach a single catalog to an already-running kernel session.
    /// Uses <c>CREATE OR REPLACE SECRET</c> so the command is safe even if an earlier partial
    /// attach left secrets behind. The catalog name is always used as the secret-name suffix to
    /// avoid collisions when multiple catalogs are attached incrementally.
    /// </summary>
    public static string GenerateAttachScript(ResolvedCatalog catalog)
    {
        var sb = new StringBuilder();
        AppendPreamble(sb);

        if (catalog.StorageConnectionString is not null)
            AppendAzureSetup(sb);

        AppendCatalogSecrets(sb, catalog, $"_{catalog.Name}", createOrReplace: true);

        return sb.ToString();
    }

    /// <summary>
    /// Generates a Python script to detach a catalog from the DuckDB session.
    /// </summary>
    public static string GenerateDetachScript(string catalogName) =>
        $"import duckdb; duckdb.execute(\"DETACH {catalogName}\")";

    private static void AppendPreamble(StringBuilder sb)
    {
        sb.AppendLine("import duckdb");
        sb.AppendLine("duckdb.execute(\"INSTALL ducklake\")");
        sb.AppendLine("duckdb.execute(\"LOAD ducklake\")");
    }

    private static void AppendAzureSetup(StringBuilder sb)
    {
        sb.AppendLine("duckdb.execute(\"INSTALL azure\")");
        sb.AppendLine("duckdb.execute(\"LOAD azure\")");
        // On Linux, this should solve the documented certificate issue:
        // https://duckdb.org/docs/current/core_extensions/azure
        sb.AppendLine("duckdb.execute(\"SET azure_transport_option_type = 'curl'\")");
    }

    private static void AppendCatalogSecrets(StringBuilder sb, ResolvedCatalog catalog, string secretSuffix, bool createOrReplace)
    {
        var keyword = createOrReplace ? "CREATE OR REPLACE SECRET" : "CREATE SECRET";

        if (catalog.StorageConnectionString is not null)
        {
            var azureSecret = $"{keyword} secret{secretSuffix}_storage (" +
                $"TYPE azure, " +
                $"CONNECTION_STRING '{EscapeSql(catalog.StorageConnectionString)}', " +
                $"SCOPE '{GetAzureScope(catalog.DataPath)}'" +
                $")";
            sb.AppendLine($"duckdb.execute(\"\"\"{EscapePython(azureSecret)}\"\"\")");
        }

        var pgSecret = $"{keyword} secret{secretSuffix}_pg (" +
            $"TYPE postgres, " +
            $"HOST '{EscapeSql(catalog.CatalogHost)}', " +
            $"PORT {catalog.CatalogPort}, " +
            $"DATABASE '{EscapeSql(catalog.CatalogDatabase)}', " +
            $"USER '{EscapeSql(catalog.CatalogUser)}', " +
            $"PASSWORD '{EscapeSql(catalog.CatalogPassword)}', " +
            $"SCOPE 'postgres://{EscapeSql(catalog.CatalogHost)}:{catalog.CatalogPort}/{EscapeSql(catalog.CatalogDatabase)}'" +
            $")";
        sb.AppendLine($"duckdb.execute(\"\"\"{EscapePython(pgSecret)}\"\"\")");

        sb.AppendLine(
            $"duckdb.execute(\"ATTACH 'ducklake:postgres:' AS {catalog.Name} " +
            $"(DATA_PATH '{EscapeSql(catalog.DataPath)}', META_SECRET 'secret{secretSuffix}_pg', AUTOMATIC_MIGRATION true)\")");
    }

    private static string EscapeSql(string value) => value.Replace("'", "''");

    /// <summary>Escapes backslashes and triple double-quotes so values are safe inside Python triple-quoted strings.</summary>
    private static string EscapePython(string value) => value.Replace("\\", "\\\\").Replace("\"\"\"", "\\\"\\\"\\\"");

    private static string GetAzureScope(string dataPath)
    {
        // Extract the container-level scope from the data path
        // e.g., abfss://container@account.dfs.core.windows.net/path → az://account.dfs.core.windows.net/container
        if (dataPath.StartsWith("abfss://", StringComparison.OrdinalIgnoreCase) ||
            dataPath.StartsWith("az://", StringComparison.OrdinalIgnoreCase))
        {
            return dataPath;
        }

        return "az://";
    }
}
