using Microsoft.AspNetCore.Authorization;

namespace Datateal.Ui.Shared.Workspaces;

/// <summary>
/// Authorization requirement satisfied when the user is a tenant Admin, a WorkspaceAdmin
/// of the active workspace, or holds one of <see cref="AllowedRoles"/> within the active
/// workspace.
/// </summary>
public sealed class WorkspaceScopedRoleRequirement(params string[] allowedRoles) : IAuthorizationRequirement
{
    public IReadOnlyCollection<string> AllowedRoles { get; } = allowedRoles;
}
