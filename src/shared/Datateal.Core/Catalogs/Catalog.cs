using System.Text.RegularExpressions;

namespace Datateal.Core.Catalogs;

/// <summary>
/// Abstract base for all catalog variants.
/// Must be a valid DuckDB database name/alias (alphanumeric + underscores, no leading digit).
/// </summary>
public abstract class Catalog
{
    private static readonly Regex ValidIdentifier = new("^[a-zA-Z_][a-zA-Z0-9_]*$", RegexOptions.Compiled);

    public Guid Id { get; set; }

    public CatalogType CatalogType { get; protected set; }

    /// <summary>
    /// Must be a valid DuckDB identifier and PostgreSQL database name.
    /// Unique across all catalogs.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// When <c>true</c> (the default), the catalog is accessible from every workspace.
    /// When <c>false</c>, access is limited to the workspaces listed in
    /// <see cref="WorkspaceAccessList"/>.
    /// </summary>
    public bool AccessibleFromAllWorkspaces { get; set; } = true;

    /// <summary>
    /// Explicit per-workspace access grants (only relevant when
    /// <see cref="AccessibleFromAllWorkspaces"/> is <c>false</c>).
    /// </summary>
    public List<CatalogWorkspaceAccess> WorkspaceAccessList { get; set; } = [];

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public static bool IsValidName(string name) => ValidIdentifier.IsMatch(name);

    public static void ValidateName(string name)
    {
        if (!IsValidName(name))
            throw new ArgumentException($"Catalog name '{name}' is not a valid identifier. Names must match [a-zA-Z_][a-zA-Z0-9_]*.", nameof(name));
    }
}
