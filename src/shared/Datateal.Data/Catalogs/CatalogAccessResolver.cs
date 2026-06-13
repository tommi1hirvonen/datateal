using Microsoft.EntityFrameworkCore;

namespace Datateal.Data.Catalogs;

/// <inheritdoc cref="ICatalogAccessResolver" />
public class CatalogAccessResolver(DatatealDbContext db) : ICatalogAccessResolver
{
    public async Task<IReadOnlySet<Guid>?> GetAccessibleCatalogIdsAsync(Guid? userId, Guid? workspaceId, CancellationToken ct = default)
    {
        var userSet = await GetUserAccessibleIdsAsync(userId, ct);
        var workspaceSet = await GetWorkspaceAccessibleIdsAsync(workspaceId, ct);
        return CatalogAccessRules.Combine(userSet, workspaceSet);
    }

    public async Task<bool> HasAccessByIdAsync(Guid? userId, Guid? workspaceId, Guid catalogId, CancellationToken ct = default)
    {
        var accessibleIds = await GetAccessibleCatalogIdsAsync(userId, workspaceId, ct);
        return accessibleIds is null || accessibleIds.Contains(catalogId);
    }

    public async Task<bool> HasAccessByNameAsync(Guid? userId, Guid? workspaceId, string catalogName, CancellationToken ct = default)
    {
        var accessibleIds = await GetAccessibleCatalogIdsAsync(userId, workspaceId, ct);
        if (accessibleIds is null)
            return true;

        var catalog = await db.Catalogs
            .Where(c => c.Name == catalogName)
            .Select(c => new { c.Id })
            .FirstOrDefaultAsync(ct);

        return catalog is not null && accessibleIds.Contains(catalog.Id);
    }

    public async Task<IReadOnlyList<string>> FilterAccessibleNamesAsync(
        Guid? userId, Guid? workspaceId, IReadOnlyList<string> catalogNames, CancellationToken ct = default)
    {
        if (catalogNames.Count == 0)
            return catalogNames;

        var accessibleIds = await GetAccessibleCatalogIdsAsync(userId, workspaceId, ct);
        if (accessibleIds is null)
            return catalogNames;

        return await db.Catalogs
            .Where(c => catalogNames.Contains(c.Name) && accessibleIds.Contains(c.Id))
            .Select(c => c.Name)
            .ToListAsync(ct);
    }

    /// <summary>
    /// User-level access. Delegates the decision to <see cref="CatalogAccessRules.ResolveUserTier"/>;
    /// this method only loads the user's attributes.
    /// </summary>
    private async Task<IReadOnlySet<Guid>?> GetUserAccessibleIdsAsync(Guid? userId, CancellationToken ct)
    {
        if (userId is null)
            return CatalogAccessRules.ResolveUserTier(null, null);

        var profile = await db.AppUsers
            .Where(u => u.Id == userId)
            .Select(u => new CatalogAccessRules.UserCatalogProfile(
                u.Roles,
                u.HasAllCatalogAccess,
                u.CatalogAccessList.Select(a => a.CatalogId).ToList()))
            .FirstOrDefaultAsync(ct);

        return CatalogAccessRules.ResolveUserTier(userId, profile);
    }

    /// <summary>
    /// Workspace-level access. Delegates the decision to
    /// <see cref="CatalogAccessRules.ResolveWorkspaceTier"/>; this method only loads the catalog ids.
    /// </summary>
    private async Task<IReadOnlySet<Guid>?> GetWorkspaceAccessibleIdsAsync(Guid? workspaceId, CancellationToken ct)
    {
        if (workspaceId is null)
            return null;

        var openIds = await db.Catalogs
            .Where(c => c.AccessibleFromAllWorkspaces)
            .Select(c => c.Id)
            .ToListAsync(ct);

        var grantedIds = await db.CatalogWorkspaceAccess
            .Where(a => a.WorkspaceId == workspaceId)
            .Select(a => a.CatalogId)
            .ToListAsync(ct);

        return CatalogAccessRules.ResolveWorkspaceTier(workspaceId, openIds, grantedIds);
    }
}
