using System.Security.Claims;
using Datateal.Auth;
using Datateal.Data;
using Datateal.Ui.Server.Core.Catalogs;
using Microsoft.EntityFrameworkCore;

namespace Datateal.Ui.Server.Infrastructure;

internal class CatalogAccessService(DatatealDbContext db) : ICatalogAccessService
{
    public async Task<IReadOnlySet<Guid>?> GetAccessibleCatalogIdsAsync(ClaimsPrincipal user, Guid? workspaceId, CancellationToken ct = default)
    {
        var userSet = await GetUserAccessibleIdsAsync(user, ct);
        var workspaceSet = await GetWorkspaceAccessibleIdsAsync(workspaceId, ct);
        return Combine(userSet, workspaceSet);
    }

    public async Task<bool> HasAccessAsync(ClaimsPrincipal user, Guid? workspaceId, Guid catalogId, CancellationToken ct = default)
    {
        var accessibleIds = await GetAccessibleCatalogIdsAsync(user, workspaceId, ct);
        return accessibleIds is null || accessibleIds.Contains(catalogId);
    }

    public async Task<bool> HasAccessByNameAsync(ClaimsPrincipal user, Guid? workspaceId, string catalogName, CancellationToken ct = default)
    {
        var accessibleIds = await GetAccessibleCatalogIdsAsync(user, workspaceId, ct);
        if (accessibleIds is null)
            return true;

        var catalog = await db.Catalogs
            .Where(c => c.Name == catalogName)
            .Select(c => new { c.Id })
            .FirstOrDefaultAsync(ct);

        return catalog is not null && accessibleIds.Contains(catalog.Id);
    }

    public async Task<IReadOnlyList<string>> FilterAccessibleNamesAsync(
        ClaimsPrincipal user, Guid? workspaceId, IReadOnlyList<string> catalogNames, CancellationToken ct = default)
    {
        if (catalogNames.Count == 0)
            return catalogNames;

        var accessibleIds = await GetAccessibleCatalogIdsAsync(user, workspaceId, ct);
        if (accessibleIds is null)
            return catalogNames;

        return await db.Catalogs
            .Where(c => catalogNames.Contains(c.Name) && accessibleIds.Contains(c.Id))
            .Select(c => c.Name)
            .ToListAsync(ct);
    }

    /// <summary>
    /// User-level access: <c>null</c> means unrestricted (Admin, CatalogContributor, or the
    /// <see cref="Core.Users.AppUser.HasAllCatalogAccess"/> flag); otherwise the explicit grants.
    /// </summary>
    private async Task<IReadOnlySet<Guid>?> GetUserAccessibleIdsAsync(ClaimsPrincipal user, CancellationToken ct)
    {
        if (HasUnrestrictedRoles(user))
            return null;

        var appUser = await ResolveUserAsync(user, ct);
        if (appUser is null || appUser.HasAllCatalogAccess)
            return null;

        return appUser.CatalogAccessList.Select(a => a.CatalogId).ToHashSet();
    }

    /// <summary>
    /// Workspace-level access: catalogs flagged accessible from all workspaces plus those
    /// explicitly granted to <paramref name="workspaceId"/>. <c>null</c> when no workspace is
    /// in scope (no restriction applied).
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

        return openIds.Concat(grantedIds).ToHashSet();
    }

    private static IReadOnlySet<Guid>? Combine(IReadOnlySet<Guid>? a, IReadOnlySet<Guid>? b)
    {
        if (a is null) return b;
        if (b is null) return a;
        return a.Where(b.Contains).ToHashSet();
    }

    private static bool HasUnrestrictedRoles(ClaimsPrincipal user) =>
        user.IsInRole(DatatealRole.Admin) || user.IsInRole(DatatealRole.CatalogContributor);

    private async Task<UserWithCatalogAccess?> ResolveUserAsync(ClaimsPrincipal user, CancellationToken ct)
    {
        var externalId = user.FindFirstValue("http://schemas.microsoft.com/identity/claims/objectidentifier")
            ?? user.FindFirstValue("oid");
        var email = user.FindFirstValue("preferred_username")
            ?? user.FindFirstValue(ClaimTypes.Email)
            ?? user.FindFirstValue("email");

        UserWithCatalogAccess? appUser = null;

        if (externalId is not null)
        {
            appUser = await db.AppUsers
                .Where(u => u.ExternalId == externalId)
                .Select(u => new UserWithCatalogAccess(
                    u.HasAllCatalogAccess,
                    u.CatalogAccessList.Select(a => new CatalogAccessEntry(a.CatalogId)).ToList()))
                .FirstOrDefaultAsync(ct);
        }

        if (appUser is null && email is not null)
        {
            appUser = await db.AppUsers
                .Where(u => u.Email == email)
                .Select(u => new UserWithCatalogAccess(
                    u.HasAllCatalogAccess,
                    u.CatalogAccessList.Select(a => new CatalogAccessEntry(a.CatalogId)).ToList()))
                .FirstOrDefaultAsync(ct);
        }

        return appUser;
    }

    private record UserWithCatalogAccess(bool HasAllCatalogAccess, List<CatalogAccessEntry> CatalogAccessList);
    private record CatalogAccessEntry(Guid CatalogId);
}
