using Datateal.Auth;
using Microsoft.AspNetCore.Authorization;

namespace Datateal.Ui.Shared.Workspaces;

/// <summary>
/// Evaluates <see cref="WorkspaceScopedRoleRequirement"/> against the active workspace.
/// A tenant Admin satisfies every workspace-scoped policy. Otherwise the user must hold
/// WorkspaceAdmin or one of the requirement's allowed roles within the active workspace.
/// </summary>
public sealed class WorkspaceScopedRoleHandler(IActiveWorkspaceAccessor activeWorkspaceAccessor)
    : AuthorizationHandler<WorkspaceScopedRoleRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, WorkspaceScopedRoleRequirement requirement)
    {
        var user = context.User;

        if (user.Identity?.IsAuthenticated != true)
            return Task.CompletedTask;

        // Tenant admins can act in any workspace.
        if (user.IsInRole(DatatealRole.Admin))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var workspaceId = activeWorkspaceAccessor.ActiveWorkspaceId;
        if (workspaceId is null)
            return Task.CompletedTask;

        var rolesInWorkspace = WorkspaceRoleClaims.GetRoles(user, workspaceId.Value)
            .ToHashSet(StringComparer.Ordinal);

        if (rolesInWorkspace.Contains(DatatealRole.WorkspaceAdmin)
            || rolesInWorkspace.Overlaps(requirement.AllowedRoles))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
