namespace DuckHouse.Orchestrator.Core.Interfaces;

/// <summary>
/// Resolved catalog connection details for kernel setup.
/// </summary>
public record ResolvedCatalog(
    string Name,
    string DataPath,
    string? StorageConnectionString,
    string CatalogHost,
    int CatalogPort,
    string CatalogDatabase,
    string CatalogUser,
    string CatalogPassword);

/// <summary>
/// Resolves catalog names into decrypted connection details for kernel sessions.
/// </summary>
public interface ICatalogResolver
{
    Task<IReadOnlyList<ResolvedCatalog>> ResolveAsync(IReadOnlyList<string> catalogNames, CancellationToken ct = default);
}
