namespace Datateal.Auth;

/// <summary>
/// Authorization policy name constants.
/// </summary>
public static class AuthPolicy
{
    public const string Admin = nameof(Admin);
    public const string NodePoolManage = nameof(NodePoolManage);
    public const string NodePoolOperate = nameof(NodePoolOperate);
    public const string JobManage = nameof(JobManage);
    public const string JobOperate = nameof(JobOperate);
    public const string JobRead = nameof(JobRead);
    public const string WorkspaceManage = nameof(WorkspaceManage);
    public const string WorkspaceRead = nameof(WorkspaceRead);
    public const string CatalogManage = nameof(CatalogManage);
    public const string EnvironmentManage = nameof(EnvironmentManage);

    /// <summary>
    /// Manage user memberships and roles within the active workspace.
    /// Satisfied by a tenant Admin or a WorkspaceAdmin of the active workspace.
    /// </summary>
    public const string WorkspaceMembershipManage = nameof(WorkspaceMembershipManage);
}
