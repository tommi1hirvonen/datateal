namespace DuckHouse.Core.Catalogs;

/// <summary>
/// Abstract base for all catalog variants.
/// Must be a valid DuckDB database name/alias (alphanumeric + underscores, no leading digit).
/// </summary>
public abstract class Catalog
{
    public Guid Id { get; set; }

    /// <summary>
    /// Must be a valid DuckDB identifier and PostgreSQL database name.
    /// Unique across all catalogs.
    /// </summary>
    public required string Name { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
