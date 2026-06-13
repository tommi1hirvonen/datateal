using Datateal.Core.Catalogs;

namespace Datateal.Ui.Server.Core.Repositories;

public interface ICatalogRepository
{
    Task<IReadOnlyList<Catalog>> GetAllAsync(CancellationToken ct = default);
    Task<Catalog?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Catalog?> GetByNameAsync(string name, CancellationToken ct = default);
    Task<bool> CatalogNameExistsAsync(string name, Guid? excludeId = null, CancellationToken ct = default);
    Task<Catalog> CreateAsync(Catalog catalog, CancellationToken ct = default);
    Task<Catalog?> UpdateAsync(Catalog catalog, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Catalog>> GetByNamesAsync(IReadOnlyList<string> names, CancellationToken ct = default);

    /// <summary>Catalog names from <paramref name="names"/> that the given workspace may use.</summary>
    Task<IReadOnlyList<string>> GetWorkspaceAccessibleNamesAsync(Guid workspaceId, IReadOnlyList<string> names, CancellationToken ct = default);

    /// <summary>Returns a catalog's workspace-access configuration.</summary>
    Task<(bool AccessibleFromAllWorkspaces, IReadOnlyList<Guid> WorkspaceIds)?> GetWorkspaceAccessAsync(Guid catalogId, CancellationToken ct = default);

    /// <summary>Replaces a catalog's workspace-access configuration.</summary>
    Task<bool> SetWorkspaceAccessAsync(Guid catalogId, bool accessibleFromAllWorkspaces, IReadOnlyList<Guid> workspaceIds, CancellationToken ct = default);
}
