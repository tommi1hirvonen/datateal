using System.Text;
using DuckHouse.Ui.Shared.Catalogs;

namespace DuckHouse.Ui.Client.Services;

/// <summary>
/// Generates DuckDB setup commands for attaching DuckLake catalogs to a kernel session.
/// </summary>
public static class CatalogSetupGenerator
{
    /// <summary>
    /// Generates the complete setup script for the given resolved catalogs.
    /// </summary>
    public static string GenerateSetupScript(IReadOnlyList<ResolvedCatalogDto> catalogs, bool isLinux = false)
    {
        if (catalogs.Count == 0) return string.Empty;

        var sb = new StringBuilder();

        // Extension installations
        sb.AppendLine("INSTALL ducklake;");
        sb.AppendLine("LOAD ducklake;");

        var anyAzure = catalogs.Any(c => c.StorageConnectionString is not null);
        if (anyAzure)
        {
            sb.AppendLine("INSTALL azure;");
            sb.AppendLine("LOAD azure;");

            if (isLinux)
                sb.AppendLine("SET azure_transport_option_type = 'curl';");
        }

        // Secrets and attachments for each catalog
        for (var i = 0; i < catalogs.Count; i++)
        {
            var catalog = catalogs[i];
            var suffix = catalogs.Count > 1 ? $"_{catalog.Name}" : "";

            // Azure storage secret (if applicable)
            if (catalog.StorageConnectionString is not null)
            {
                sb.AppendLine($"""
                    CREATE SECRET secret{suffix}_storage (
                        TYPE azure,
                        CONNECTION_STRING '{EscapeSql(catalog.StorageConnectionString)}',
                        SCOPE '{GetAzureScope(catalog.DataPath)}'
                    );
                    """);
            }

            // Postgres catalog secret
            sb.AppendLine($"""
                CREATE SECRET secret{suffix}_pg (
                    TYPE postgres,
                    HOST '{EscapeSql(catalog.CatalogHost)}',
                    PORT {catalog.CatalogPort},
                    DATABASE '{EscapeSql(catalog.CatalogDatabase)}',
                    USER '{EscapeSql(catalog.CatalogUser)}',
                    PASSWORD '{EscapeSql(catalog.CatalogPassword)}',
                    SCOPE 'postgres://{EscapeSql(catalog.CatalogHost)}:{catalog.CatalogPort}/{EscapeSql(catalog.CatalogDatabase)}'
                );
                """);

            // Attach the catalog
            sb.AppendLine($"ATTACH 'ducklake:postgres:' AS {catalog.Name} (DATA_PATH '{EscapeSql(catalog.DataPath)}');");
        }

        return sb.ToString();
    }

    private static string EscapeSql(string value) => value.Replace("'", "''");

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
