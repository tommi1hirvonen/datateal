using System.Security.Claims;
using Datateal.Auth;
using Datateal.Data;
using Datateal.Data.Catalogs;
using Datateal.Ui.Server.Core.Catalogs;
using Microsoft.EntityFrameworkCore;

namespace Datateal.Ui.Server.Infrastructure;

/// <summary>
/// Adapts the <see cref="ClaimsPrincipal"/>-based interactive API surface onto the shared
/// <see cref="ICatalogAccessResolver"/> so the UI and the orchestrator share one catalog-access
/// implementation. The principal is resolved to an <see cref="Core.Users.AppUser.Id"/> (or treated as
/// unrestricted when it carries an Admin/CatalogContributor role) and the rest is delegated.
/// </summary>
internal class CatalogAccessService(DatatealDbContext db, ICatalogAccessResolver resolver) : ICatalogAccessService
{
    public async Task<IReadOnlySet<Guid>?> GetAccessibleCatalogIdsAsync(ClaimsPrincipal user, Guid? workspaceId, CancellationToken ct = default)
    {
        var userId = await ResolveEffectiveUserIdAsync(user, ct);
        return await resolver.GetAccessibleCatalogIdsAsync(userId, workspaceId, ct);
    }

    public async Task<bool> HasAccessAsync(ClaimsPrincipal user, Guid? workspaceId, Guid catalogId, CancellationToken ct = default)
    {
        var userId = await ResolveEffectiveUserIdAsync(user, ct);
        return await resolver.HasAccessByIdAsync(userId, workspaceId, catalogId, ct);
    }

    public async Task<bool> HasAccessByNameAsync(ClaimsPrincipal user, Guid? workspaceId, string catalogName, CancellationToken ct = default)
    {
        var userId = await ResolveEffectiveUserIdAsync(user, ct);
        return await resolver.HasAccessByNameAsync(userId, workspaceId, catalogName, ct);
    }

    public async Task<IReadOnlyList<string>> FilterAccessibleNamesAsync(
        ClaimsPrincipal user, Guid? workspaceId, IReadOnlyList<string> catalogNames, CancellationToken ct = default)
    {
        var userId = await ResolveEffectiveUserIdAsync(user, ct);
        return await resolver.FilterAccessibleNamesAsync(userId, workspaceId, catalogNames, ct);
    }

    /// <summary>
    /// Maps the principal to the user id the shared resolver expects. Returns <c>null</c> when the
    /// principal holds an unrestricted role (no user-level restriction) or cannot be matched to a
    /// stored user (preserving the prior "unknown principal is unrestricted at the user tier" behavior;
    /// access is still bounded by the workspace tier).
    /// </summary>
    private async Task<Guid?> ResolveEffectiveUserIdAsync(ClaimsPrincipal user, CancellationToken ct)
    {
        if (HasUnrestrictedRoles(user))
            return null;

        var externalId = user.FindFirstValue("http://schemas.microsoft.com/identity/claims/objectidentifier")
            ?? user.FindFirstValue("oid");
        var email = user.FindFirstValue("preferred_username")
            ?? user.FindFirstValue(ClaimTypes.Email)
            ?? user.FindFirstValue("email");

        Guid? id = null;

        if (externalId is not null)
        {
            id = await db.AppUsers
                .Where(u => u.ExternalId == externalId)
                .Select(u => (Guid?)u.Id)
                .FirstOrDefaultAsync(ct);
        }

        if (id is null && email is not null)
        {
            id = await db.AppUsers
                .Where(u => u.Email == email)
                .Select(u => (Guid?)u.Id)
                .FirstOrDefaultAsync(ct);
        }

        return id;
    }

    private static bool HasUnrestrictedRoles(ClaimsPrincipal user) =>
        user.IsInRole(DatatealRole.Admin) || user.IsInRole(DatatealRole.CatalogContributor);
}
