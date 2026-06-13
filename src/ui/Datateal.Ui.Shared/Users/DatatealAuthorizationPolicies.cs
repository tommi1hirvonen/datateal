using Datateal.Auth;
using Datateal.Ui.Shared.Workspaces;
using Microsoft.AspNetCore.Authorization;

namespace Datateal.Ui.Shared.Users;

/// <summary>
/// Shared authorization policy configuration used by both the server and WASM client.
/// Tenant-global policies use role claims directly; workspace-scoped policies use
/// <see cref="WorkspaceScopedRoleRequirement"/> resolved against the active workspace.
/// </summary>
public static class DatatealAuthorizationPolicies
{
    public static void Configure(AuthorizationOptions options)
    {
        // ── Tenant-global ─────────────────────────────────────────────────
        options.AddPolicy(AuthPolicy.Admin, p =>
            p.RequireRole(DatatealRole.Admin));
        options.AddPolicy(AuthPolicy.CatalogManage, p =>
            p.RequireRole(DatatealRole.Admin, DatatealRole.CatalogContributor));

        // ── Workspace-scoped ──────────────────────────────────────────────
        AddWorkspacePolicy(options, AuthPolicy.NodePoolManage,
            DatatealRole.NodePoolContributor);
        AddWorkspacePolicy(options, AuthPolicy.NodePoolOperate,
            DatatealRole.NodePoolContributor, DatatealRole.NodePoolOperator);
        AddWorkspacePolicy(options, AuthPolicy.JobManage,
            DatatealRole.JobContributor);
        AddWorkspacePolicy(options, AuthPolicy.JobOperate,
            DatatealRole.JobContributor, DatatealRole.JobOperator);
        AddWorkspacePolicy(options, AuthPolicy.JobRead,
            DatatealRole.JobContributor, DatatealRole.JobOperator, DatatealRole.JobReader);
        AddWorkspacePolicy(options, AuthPolicy.WorkspaceManage,
            DatatealRole.WorkspaceContributor);
        AddWorkspacePolicy(options, AuthPolicy.WorkspaceRead,
            DatatealRole.WorkspaceContributor, DatatealRole.WorkspaceReader);
        AddWorkspacePolicy(options, AuthPolicy.EnvironmentManage,
            DatatealRole.EnvironmentManager);

        // Managing memberships requires Admin or WorkspaceAdmin (no extra roles).
        AddWorkspacePolicy(options, AuthPolicy.WorkspaceMembershipManage);
    }

    private static void AddWorkspacePolicy(AuthorizationOptions options, string policy, params string[] roles) =>
        options.AddPolicy(policy, p =>
        {
            p.RequireAuthenticatedUser();
            p.Requirements.Add(new WorkspaceScopedRoleRequirement(roles));
        });
}
