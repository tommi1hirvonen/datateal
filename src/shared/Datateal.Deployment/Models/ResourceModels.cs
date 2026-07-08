namespace Datateal.Deployment.Models;

// ── Admin-scope resource models ───────────────────────────────────────────────

/// <summary>
/// Catalog resource definition. <c>type</c> = <c>managed</c> or <c>unmanaged</c>.
/// </summary>
public class CatalogModel
{
    public string Name { get; set; } = "";
    /// <summary><c>managed</c> or <c>unmanaged</c>.</summary>
    public string Type { get; set; } = "managed";
    public string? Description { get; set; }
    public bool? AccessibleFromAllWorkspaces { get; set; }

    // Managed catalog fields
    public string? DataPath { get; set; }

    // Unmanaged catalog fields
    public string? CatalogHost { get; set; }
    public string? CatalogDatabase { get; set; }
    public string? CatalogUser { get; set; }

    /// <summary>
    /// Workspaces that can access this catalog (empty = use AccessibleFromAllWorkspaces).
    /// </summary>
    public List<string>? WorkspaceAccess { get; set; }

    // Unmanaged catalog credentials
    /// <summary>
    /// Password for the catalog's Postgres metadata database. Required when creating a new
    /// unmanaged catalog; optional on updates (omit to preserve the existing password).
    /// Never included in exported bundles.
    /// </summary>
    public string? CatalogPassword { get; set; }
}

/// <summary>Workspace resource definition.</summary>
public class WorkspaceModel
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
}

/// <summary>
/// Declares workspace memberships. Maps a list of users to their roles
/// within the named workspace.
/// </summary>
public class WorkspaceMembershipModel
{
    public string Workspace { get; set; } = "";
    public List<WorkspaceMemberEntry> Members { get; set; } = [];
}

public class WorkspaceMemberEntry
{
    public string Email { get; set; } = "";
    public List<string> Roles { get; set; } = [];
}

/// <summary>
/// User-level catalog access grant (restricts which catalogs the user can reach).
/// </summary>
public class UserCatalogAccessModel
{
    public string Email { get; set; } = "";
    public bool HasAllCatalogAccess { get; set; } = true;
    /// <summary>When <c>HasAllCatalogAccess</c> is false, the explicit allow-list.</summary>
    public List<string>? AllowedCatalogs { get; set; }
}

// ── Workspace-scope resource models ──────────────────────────────────────────

/// <summary>Folder entry in the workspace folder tree.</summary>
public class FolderModel
{
    public string Path { get; set; } = "";
}

/// <summary>Notebook resource definition.</summary>
public class NotebookModel
{
    /// <summary>Logical path inside the workspace (e.g. <c>etl/load_sales</c>).</summary>
    public string Path { get; set; } = "";
    /// <summary>Relative path inside the bundle's <c>src/notebooks/</c> directory.</summary>
    public string? SourceFile { get; set; }
    /// <summary>Catalog names attached to this notebook.</summary>
    public List<string>? Catalogs { get; set; }
}

/// <summary>SQL query resource definition.</summary>
public class QueryModel
{
    public string Path { get; set; } = "";
    public string? SourceFile { get; set; }
    public List<string>? Catalogs { get; set; }
}

/// <summary>
/// Node pool definition. <c>type</c> = <c>interactive</c> or <c>job</c>.
/// </summary>
public class NodePoolModel
{
    public string Name { get; set; } = "";
    /// <summary><c>interactive</c> or <c>job</c>.</summary>
    public string Type { get; set; } = "job";
    public string VmSize { get; set; } = "";
    public string? KernelRequirements { get; set; }
    public string? Description { get; set; }

    // Job-pool specific
    public int? WarmNodes { get; set; }
    public int? MaxNodes { get; set; }
    public string? NodeAcquireTimeout { get; set; }
    public string? NodeIdleTimeout { get; set; }

    // Attached resources (names resolved from workspace)
    public List<string>? WheelPackages { get; set; }
    public List<string>? EnvironmentVariables { get; set; }
    public List<string>? Secrets { get; set; }
}

/// <summary>Environment variable definition.</summary>
public class EnvironmentVariableModel
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
    public string? Description { get; set; }
}

/// <summary>
/// Secret metadata. Values are never stored in the bundle; supply them at deploy
/// time via <c>${var.NAME}</c> or <c>${env.NAME}</c> substitution.
/// </summary>
public class SecretModel
{
    public string Key { get; set; } = "";
    public string? Description { get; set; }
    /// <summary>
    /// If present, the secret value expression (e.g. <c>${env.MY_SECRET}</c>).
    /// Omit when the intent is to leave an existing secret's value unchanged.
    /// </summary>
    public string? Value { get; set; }
}

/// <summary>Wheel package (binary blob) reference in the bundle.</summary>
public class WheelPackageModel
{
    public string Name { get; set; } = "";
    public string FileName { get; set; } = "";
    /// <summary>Relative path inside the bundle's <c>files/wheels/</c> directory.</summary>
    public string? BundleFilePath { get; set; }
}
