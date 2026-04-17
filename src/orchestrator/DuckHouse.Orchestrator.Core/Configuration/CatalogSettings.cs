namespace DuckHouse.Orchestrator.Core.Configuration;

/// <summary>
/// Default connection settings for managed DuckLake catalogs.
/// Bound from the "Catalogs" configuration section.
/// Must stay in sync with <c>DuckHouse.Ui.Server.Core.Catalogs.CatalogSettings</c>.
/// </summary>
public class CatalogSettings
{
    public string BaseDataPath { get; set; } = string.Empty;
    public string? StorageConnectionString { get; set; }
    public string CatalogHost { get; set; } = "localhost";
    public int CatalogPort { get; set; } = 5432;
    public string CatalogUser { get; set; } = string.Empty;
    public string CatalogPassword { get; set; } = string.Empty;
}
