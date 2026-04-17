using System.Text;
using DuckHouse.Orchestrator.Core.Interfaces;

namespace DuckHouse.Orchestrator.Application.Catalogs;

/// <summary>
/// Generates DuckDB setup commands for attaching DuckLake catalogs to a kernel session.
/// </summary>
public static class CatalogSetupGenerator
{
    public static string GenerateSetupScript(IReadOnlyList<ResolvedCatalog> catalogs)
    {
        if (catalogs.Count == 0) return string.Empty;

        var sb = new StringBuilder();

        sb.AppendLine("INSTALL ducklake;");
        sb.AppendLine("LOAD ducklake;");

        var anyAzure = catalogs.Any(c => c.StorageConnectionString is not null);
        if (anyAzure)
        {
            sb.AppendLine("INSTALL azure;");
            sb.AppendLine("LOAD azure;");
            // Runtime containers run Linux
            sb.AppendLine("SET azure_transport_option_type = 'curl';");
        }

        for (var i = 0; i < catalogs.Count; i++)
        {
            var catalog = catalogs[i];
            var suffix = catalogs.Count > 1 ? $"_{catalog.Name}" : "";

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

            sb.AppendLine($"ATTACH 'ducklake:postgres:' AS {catalog.Name} (DATA_PATH '{EscapeSql(catalog.DataPath)}');");
        }

        return sb.ToString();
    }

    private static string EscapeSql(string value) => value.Replace("'", "''");

    private static string GetAzureScope(string dataPath)
    {
        if (dataPath.StartsWith("abfss://", StringComparison.OrdinalIgnoreCase) ||
            dataPath.StartsWith("az://", StringComparison.OrdinalIgnoreCase))
        {
            return dataPath;
        }

        return "az://";
    }
}
