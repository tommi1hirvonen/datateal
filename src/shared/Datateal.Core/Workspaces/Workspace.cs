using System.Text.RegularExpressions;

namespace Datateal.Core.Workspaces;

/// <summary>
/// A workspace is a tenant-level container that owns content (notebooks, queries,
/// folders), compute (node pools), jobs, environment variables, and secrets.
/// A single Datateal instance hosts many workspaces. Catalogs are tenant-global and
/// are not owned by a workspace; access to them is controlled per workspace.
/// </summary>
public class Workspace
{
    private static readonly Regex ValidSlug = new("^[a-z0-9][a-z0-9-]*[a-z0-9]$|^[a-z0-9]$", RegexOptions.Compiled);

    public Guid Id { get; set; }

    /// <summary>
    /// Human-friendly display name. Unique across the tenant.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// URL-safe, immutable-ish identifier used in links and (later) deployment
    /// automation. Lower/upper environments (dev/test/qa/prod) are encoded by
    /// convention in the slug (e.g. <c>sales-dev</c>, <c>sales-prod</c>).
    /// Unique across the tenant.
    /// </summary>
    public required string Slug { get; set; }

    public string? Description { get; set; }

    /// <summary>
    /// Marks the workspace that existing single-tenant data was migrated into.
    /// Exactly one workspace is the default; it cannot be deleted.
    /// </summary>
    public bool IsDefault { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public List<WorkspaceMembership> Memberships { get; set; } = [];

    public static bool IsValidSlug(string slug) => ValidSlug.IsMatch(slug);

    public static void ValidateSlug(string slug)
    {
        if (!IsValidSlug(slug))
            throw new ArgumentException(
                $"Workspace slug '{slug}' is not valid. Slugs must be lowercase alphanumeric with internal hyphens.",
                nameof(slug));
    }
}
