namespace Datateal.Data.Catalogs;

/// <summary>
/// Database-driven catalog access resolver keyed by <see cref="Datateal.Core.Users.AppUser.Id"/>.
/// Computes the effective set of catalogs a user may access as the intersection of their user-level
/// access (tenant roles, the <c>HasAllCatalogAccess</c> flag, or explicit per-user grants) and the
/// workspace-level access (catalogs open to all workspaces plus explicit per-workspace grants).
///
/// This is the single source of truth shared by the interactive UI path (where a
/// <see cref="System.Security.Claims.ClaimsPrincipal"/> is first resolved to a user id) and the job
/// execution path in the orchestrator (where the run's stored owner id is used).
/// </summary>
public interface ICatalogAccessResolver
{
    /// <summary>
    /// Returns <c>null</c> when access is unrestricted at both tiers; otherwise the set of catalog ids
    /// the user may access. A non-null but empty set means the user can access no catalogs.
    /// </summary>
    /// <param name="userId">
    /// The acting user's id. <c>null</c> represents an unidentifiable principal and applies no
    /// user-level restriction. A non-null id that does not resolve to an existing user yields an empty
    /// set (fail closed) so deleted owners cannot retain access.
    /// </param>
    Task<IReadOnlySet<Guid>?> GetAccessibleCatalogIdsAsync(Guid? userId, Guid? workspaceId, CancellationToken ct = default);

    /// <summary>Returns <c>true</c> if the user may access the catalog with the given id.</summary>
    Task<bool> HasAccessByIdAsync(Guid? userId, Guid? workspaceId, Guid catalogId, CancellationToken ct = default);

    /// <summary>Returns <c>true</c> if the user may access the catalog with the given name.</summary>
    Task<bool> HasAccessByNameAsync(Guid? userId, Guid? workspaceId, string catalogName, CancellationToken ct = default);

    /// <summary>
    /// Filters <paramref name="catalogNames"/> to only those the user may access. Returns the input
    /// unchanged when access is unrestricted.
    /// </summary>
    Task<IReadOnlyList<string>> FilterAccessibleNamesAsync(Guid? userId, Guid? workspaceId, IReadOnlyList<string> catalogNames, CancellationToken ct = default);
}
