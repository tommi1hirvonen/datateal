namespace Datateal.Ui.Shared.Workspaces;

/// <summary>A workspace as exposed to the client.</summary>
public record WorkspaceDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsDefault);

/// <summary>A user's membership (per-workspace roles) within a workspace.</summary>
public record WorkspaceMemberDto(
    Guid UserId,
    string Email,
    string DisplayName,
    IReadOnlyList<string> Roles);

public record CreateWorkspaceRequest(string Name, string? Description);

public record UpdateWorkspaceRequest(string Name, string? Description);

/// <summary>Creates or replaces a user's membership roles in a workspace.</summary>
public record SetWorkspaceMemberRequest(Guid UserId, IReadOnlyList<string> Roles);
