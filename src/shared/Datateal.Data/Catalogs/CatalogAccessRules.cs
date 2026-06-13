using Datateal.Auth;

namespace Datateal.Data.Catalogs;

/// <summary>
/// Pure decision logic for catalog access, separated from data loading so it can be unit tested without
/// a database. <c>null</c> consistently denotes "unrestricted" at a given tier; a non-null set is the
/// exact set of allowed catalog ids.
/// </summary>
public static class CatalogAccessRules
{
    /// <summary>Loaded user attributes relevant to catalog access.</summary>
    public sealed record UserCatalogProfile(
        IReadOnlyCollection<string> Roles,
        bool HasAllCatalogAccess,
        IReadOnlyCollection<Guid> CatalogIds);

    /// <summary>
    /// User-level access. <c>null</c> = unrestricted (no user id, or an Admin/CatalogContributor role,
    /// or the all-catalogs flag). When a user id was supplied but no profile was found, returns an empty
    /// set (fail closed) so a deleted user retains no access.
    /// </summary>
    public static IReadOnlySet<Guid>? ResolveUserTier(Guid? userId, UserCatalogProfile? profile)
    {
        if (userId is null)
            return null;

        if (profile is null)
            return new HashSet<Guid>();

        if (profile.Roles.Contains(DatatealRole.Admin) || profile.Roles.Contains(DatatealRole.CatalogContributor))
            return null;

        if (profile.HasAllCatalogAccess)
            return null;

        return profile.CatalogIds.ToHashSet();
    }

    /// <summary>
    /// Workspace-level access. <c>null</c> = unrestricted (no workspace in scope); otherwise catalogs
    /// open to all workspaces unioned with those explicitly granted to the workspace.
    /// </summary>
    public static IReadOnlySet<Guid>? ResolveWorkspaceTier(
        Guid? workspaceId, IEnumerable<Guid> openCatalogIds, IEnumerable<Guid> grantedCatalogIds)
    {
        if (workspaceId is null)
            return null;

        return openCatalogIds.Concat(grantedCatalogIds).ToHashSet();
    }

    /// <summary>Intersects two tiers, treating <c>null</c> as "no restriction".</summary>
    public static IReadOnlySet<Guid>? Combine(IReadOnlySet<Guid>? a, IReadOnlySet<Guid>? b)
    {
        if (a is null) return b;
        if (b is null) return a;
        return a.Where(b.Contains).ToHashSet();
    }
}
