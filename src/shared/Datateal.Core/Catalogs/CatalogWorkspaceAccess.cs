using Datateal.Core.Workspaces;

namespace Datateal.Core.Catalogs;

/// <summary>
/// Grants a specific workspace access to a catalog whose
/// <see cref="Catalog.AccessibleFromAllWorkspaces"/> flag is <c>false</c>.
/// </summary>
public class CatalogWorkspaceAccess
{
    public Guid Id { get; set; }

    public Guid CatalogId { get; set; }
    public Catalog Catalog { get; set; } = null!;

    public Guid WorkspaceId { get; set; }
    public Workspaces.Workspace Workspace { get; set; } = null!;
}
