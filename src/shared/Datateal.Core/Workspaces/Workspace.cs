namespace Datateal.Core.Workspaces;

/// <summary>
/// A workspace is a tenant-level container that owns content (notebooks, queries,
/// folders), compute (node pools), jobs, environment variables, and secrets.
/// A single Datateal instance hosts many workspaces. Catalogs are tenant-global and
/// are not owned by a workspace; access to them is controlled per workspace.
/// </summary>
public class Workspace
{
    public Guid Id { get; set; }

    /// <summary>
    /// Human-friendly display name. Unique across the tenant, and used as the stable
    /// identifier in the planned git/deployment-automation feature (where workspaces are
    /// referenced from version-controlled definitions and content is promoted across
    /// environments), the same way job names are. Lower/upper environments
    /// (dev/test/qa/prod) are encoded by convention in the name (e.g. <c>Sales (Dev)</c>,
    /// <c>Sales (Prod)</c>). Note: in-app links use the workspace <see cref="Id"/>.
    /// </summary>
    public required string Name { get; set; }

    public string? Description { get; set; }

    /// <summary>
    /// Marks the workspace that existing single-tenant data was migrated into.
    /// Exactly one workspace is the default; it cannot be deleted.
    /// </summary>
    public bool IsDefault { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<WorkspaceMembership> Memberships { get; set; } = [];
}
