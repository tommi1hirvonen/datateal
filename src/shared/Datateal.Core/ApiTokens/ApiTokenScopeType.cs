namespace Datateal.Core.ApiTokens;

/// <summary>
/// The isolation level of an <see cref="ApiToken"/>.
/// </summary>
public enum ApiTokenScopeType
{
    /// <summary>Tenant-global token. Grants tenant-global roles (e.g. Admin, CatalogContributor).</summary>
    Admin,

    /// <summary>
    /// Token bound to exactly one workspace. Grants per-workspace roles for that workspace only,
    /// and cannot satisfy any authorization for other workspaces or tenant-global endpoints.
    /// </summary>
    Workspace,
}
