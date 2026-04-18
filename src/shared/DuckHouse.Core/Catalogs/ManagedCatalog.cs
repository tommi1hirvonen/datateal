namespace DuckHouse.Core.Catalogs;

/// <summary>
/// A managed catalog whose connection details (host, port, credentials, data path)
/// are derived entirely from server-side <see cref="CatalogSettings"/> at runtime.
/// Only the catalog <see cref="Catalog.Name"/> is stored.
/// </summary>
public sealed class ManagedCatalog : Catalog;
