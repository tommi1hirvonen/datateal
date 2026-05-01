using DuckHouse.Auth;
using Microsoft.AspNetCore.Authorization;

namespace DuckHouse.Ui.Shared.Users;

/// <summary>
/// Shared authorization policy configuration used by both the server and WASM client.
/// </summary>
public static class DuckHouseAuthorizationPolicies
{
    public static void Configure(AuthorizationOptions options)
    {
        options.AddPolicy(AuthPolicy.Admin, p =>
            p.RequireRole(DuckHouseRole.Admin));
        options.AddPolicy(AuthPolicy.NodePoolManage, p =>
            p.RequireRole(DuckHouseRole.Admin, DuckHouseRole.NodePoolContributor));
        options.AddPolicy(AuthPolicy.NodePoolOperate, p =>
            p.RequireRole(DuckHouseRole.Admin, DuckHouseRole.NodePoolContributor, DuckHouseRole.NodePoolOperator));
        options.AddPolicy(AuthPolicy.JobManage, p =>
            p.RequireRole(DuckHouseRole.Admin, DuckHouseRole.JobContributor));
        options.AddPolicy(AuthPolicy.JobOperate, p =>
            p.RequireRole(DuckHouseRole.Admin, DuckHouseRole.JobContributor, DuckHouseRole.JobOperator));
        options.AddPolicy(AuthPolicy.JobRead, p =>
            p.RequireRole(DuckHouseRole.Admin, DuckHouseRole.JobContributor, DuckHouseRole.JobOperator, DuckHouseRole.JobReader));
        options.AddPolicy(AuthPolicy.WorkspaceManage, p =>
            p.RequireRole(DuckHouseRole.Admin, DuckHouseRole.WorkspaceContributor));
        options.AddPolicy(AuthPolicy.WorkspaceRead, p =>
            p.RequireRole(DuckHouseRole.Admin, DuckHouseRole.WorkspaceContributor, DuckHouseRole.WorkspaceReader));
        options.AddPolicy(AuthPolicy.CatalogManage, p =>
            p.RequireRole(DuckHouseRole.Admin, DuckHouseRole.CatalogContributor));
        options.AddPolicy(AuthPolicy.EnvironmentManage, p =>
            p.RequireRole(DuckHouseRole.Admin, DuckHouseRole.EnvironmentManager));
    }
}
