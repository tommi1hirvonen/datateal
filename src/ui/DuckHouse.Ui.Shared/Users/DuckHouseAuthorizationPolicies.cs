using Microsoft.AspNetCore.Authorization;

namespace DuckHouse.Ui.Shared.Users;

/// <summary>
/// Shared authorization policy configuration used by both the server and WASM client.
/// </summary>
public static class DuckHouseAuthorizationPolicies
{
    public static void Configure(AuthorizationOptions options)
    {
        options.AddPolicy("Admin", p =>
            p.RequireRole(AvailableRoles.Admin));
        options.AddPolicy("NodePoolManage", p =>
            p.RequireRole(AvailableRoles.Admin, AvailableRoles.NodePoolContributor));
        options.AddPolicy("NodePoolOperate", p =>
            p.RequireRole(AvailableRoles.Admin, AvailableRoles.NodePoolContributor, AvailableRoles.NodePoolOperator));
        options.AddPolicy("JobManage", p =>
            p.RequireRole(AvailableRoles.Admin, AvailableRoles.JobContributor));
        options.AddPolicy("JobOperate", p =>
            p.RequireRole(AvailableRoles.Admin, AvailableRoles.JobContributor, AvailableRoles.JobOperator));
        options.AddPolicy("JobRead", p =>
            p.RequireRole(AvailableRoles.Admin, AvailableRoles.JobContributor, AvailableRoles.JobOperator, AvailableRoles.JobReader));
        options.AddPolicy("WorkspaceManage", p =>
            p.RequireRole(AvailableRoles.Admin, AvailableRoles.WorkspaceContributor));
        options.AddPolicy("CatalogManage", p =>
            p.RequireRole(AvailableRoles.Admin, AvailableRoles.CatalogContributor));
        options.AddPolicy("EnvironmentManage", p =>
            p.RequireRole(AvailableRoles.Admin, AvailableRoles.EnvironmentManager));
    }
}
