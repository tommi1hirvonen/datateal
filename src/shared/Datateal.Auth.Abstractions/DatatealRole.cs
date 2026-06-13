namespace Datateal.Auth;

/// <summary>
/// Application role name constants.
/// </summary>
public static class DatatealRole
{
    public const string Admin = nameof(Admin);
    public const string NodePoolContributor = nameof(NodePoolContributor);
    public const string NodePoolOperator = nameof(NodePoolOperator);
    public const string JobContributor = nameof(JobContributor);
    public const string JobOperator = nameof(JobOperator);
    public const string JobReader = nameof(JobReader);
    public const string WorkspaceAdmin = nameof(WorkspaceAdmin);
    public const string WorkspaceContributor = nameof(WorkspaceContributor);
    public const string WorkspaceReader = nameof(WorkspaceReader);
    public const string CatalogContributor = nameof(CatalogContributor);
    public const string EnvironmentManager = nameof(EnvironmentManager);

    public static readonly string[] All =
    [
        Admin,
        NodePoolContributor,
        NodePoolOperator,
        JobContributor,
        JobOperator,
        JobReader,
        WorkspaceAdmin,
        WorkspaceContributor,
        WorkspaceReader,
        CatalogContributor,
        EnvironmentManager
    ];

    /// <summary>
    /// Roles assigned at the tenant level (stored on <c>AppUser.Roles</c>). These are not
    /// scoped to a workspace.
    /// </summary>
    public static readonly string[] TenantGlobal =
    [
        Admin,
        CatalogContributor
    ];

    /// <summary>
    /// Roles assigned per workspace (stored on <c>WorkspaceMembership.Roles</c>).
    /// </summary>
    public static readonly string[] PerWorkspace =
    [
        WorkspaceAdmin,
        WorkspaceContributor,
        WorkspaceReader,
        NodePoolContributor,
        NodePoolOperator,
        JobContributor,
        JobOperator,
        JobReader,
        EnvironmentManager
    ];

    public static bool IsTenantGlobal(string role) =>
        Array.Exists(TenantGlobal, r => r == role);

    public static bool IsPerWorkspace(string role) =>
        Array.Exists(PerWorkspace, r => r == role);
}
