namespace DuckHouse.Core.Catalogs;

public class Catalog
{
    public Guid Id { get; set; }

    /// <summary>
    /// Must be a valid DuckDB database name/alias (alphanumeric + underscores, no leading digit).
    /// Unique across all catalogs.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// True = managed catalog (connection details from appsettings).
    /// False = external/unmanaged catalog (all details provided by user).
    /// </summary>
    public bool IsManaged { get; set; }

    /// <summary>
    /// Final data path for the DuckLake instance.
    /// For managed catalogs: computed as BaseDataPath/Name.
    /// For external catalogs: provided by user.
    /// </summary>
    public string? DataPath { get; set; }

    /// <summary>
    /// Azure Data Lake connection string, encrypted via ASP.NET Data Protection API.
    /// Only required when data path uses az:// or abfss:// scheme.
    /// </summary>
    public string? EncryptedStorageConnectionString { get; set; }

    public string? CatalogHost { get; set; }
    public int? CatalogPort { get; set; }

    /// <summary>
    /// PostgreSQL database name for the DuckLake metadata catalog.
    /// For managed catalogs, this equals the catalog Name.
    /// </summary>
    public string? CatalogDatabase { get; set; }

    public string? CatalogUser { get; set; }

    /// <summary>
    /// PostgreSQL password, encrypted via ASP.NET Data Protection API.
    /// </summary>
    public string? EncryptedCatalogPassword { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
