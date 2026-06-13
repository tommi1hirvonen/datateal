using Datateal.Core.Users;

namespace Datateal.Core.Workspaces;

/// <summary>
/// Grants a user a set of per-workspace roles within a single workspace.
/// Tenant-global roles (Admin, CatalogContributor) are stored on
/// <see cref="AppUser.Roles"/> and are not represented here.
/// </summary>
public class WorkspaceMembership
{
    public Guid Id { get; set; }

    public Guid WorkspaceId { get; set; }
    public Workspace Workspace { get; set; } = null!;

    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;

    /// <summary>
    /// Per-workspace application roles for this user. Stored as a JSON array
    /// (EF Core primitive collection).
    /// </summary>
    public List<string> Roles { get; set; } = [];

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
