namespace DuckHouse.Ui.Server.Core.Catalogs;

/// <summary>
/// Default connection settings for managed DuckLake catalogs.
/// Bound from the "Catalogs" configuration section.
/// </summary>
public class CatalogSettings
{
    /// <summary>
    /// Base data path for managed catalogs. Final path = BaseDataPath/CatalogName.
    /// Can be a local path or an abfss:// path for Azure Data Lake.
    /// </summary>
    public string BaseDataPath { get; set; } = string.Empty;

    /// <summary>
    /// Azure Data Lake connection string. Required when BaseDataPath uses az:// or abfss://.
    /// </summary>
    public string? StorageConnectionString { get; set; }

    /// <summary>
    /// PostgreSQL host for the DuckLake metadata catalog.
    /// </summary>
    public string CatalogHost { get; set; } = "localhost";

    /// <summary>
    /// PostgreSQL port.
    /// </summary>
    public int CatalogPort { get; set; } = 5432;

    /// <summary>
    /// PostgreSQL user.
    /// </summary>
    public string CatalogUser { get; set; } = string.Empty;

    /// <summary>
    /// PostgreSQL password.
    /// </summary>
    public string CatalogPassword { get; set; } = string.Empty;
}
