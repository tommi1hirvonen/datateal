namespace DuckHouse.Core.Catalogs;

/// <summary>
/// An external (user-managed) catalog whose connection details are stored explicitly.
/// All connection fields are required; the storage connection string and catalog password
/// are optional and stored encrypted via the ASP.NET Data Protection API.
/// </summary>
public sealed class UnmanagedCatalog : Catalog
{
    /// <summary>
    /// Final data path for the DuckLake instance (local path or az:// / abfss:// URI).
    /// </summary>
    public required string DataPath { get; set; }

    /// <summary>
    /// Azure Data Lake connection string, encrypted via ASP.NET Data Protection API.
    /// Only required when <see cref="DataPath"/> uses az:// or abfss:// scheme.
    /// </summary>
    public string? EncryptedStorageConnectionString { get; set; }

    public required string CatalogHost { get; set; }
    public required int CatalogPort { get; set; }

    /// <summary>
    /// PostgreSQL database name for the DuckLake metadata catalog.
    /// </summary>
    public required string CatalogDatabase { get; set; }

    public required string CatalogUser { get; set; }

    /// <summary>
    /// PostgreSQL password, encrypted via ASP.NET Data Protection API.
    /// </summary>
    public string? EncryptedCatalogPassword { get; set; }
}
