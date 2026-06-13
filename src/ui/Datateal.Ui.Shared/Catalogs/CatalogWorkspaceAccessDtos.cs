namespace Datateal.Ui.Shared.Catalogs;

/// <summary>
/// A catalog's workspace-access configuration. When
/// <see cref="AccessibleFromAllWorkspaces"/> is <c>true</c>, the catalog is usable from every
/// workspace and <see cref="WorkspaceIds"/> is ignored; otherwise access is limited to the
/// listed workspaces.
/// </summary>
public record CatalogWorkspaceAccessDto(bool AccessibleFromAllWorkspaces, IReadOnlyList<Guid> WorkspaceIds);

public record SetCatalogWorkspaceAccessRequest(bool AccessibleFromAllWorkspaces, IReadOnlyList<Guid> WorkspaceIds);
