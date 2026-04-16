namespace DuckHouse.Ui.Shared.Workspace;

/// <summary>
/// Result of resolving a relative workspace path.
/// <para><paramref name="Kind"/> is "notebook", "query", or "folder".</para>
/// <para><paramref name="Content"/> contains the notebook JSON or query SQL (null for folders).</para>
/// <para><paramref name="Listing"/> contains the folder's items (null for notebooks/queries).</para>
/// </summary>
public record ResolvedWorkspaceItem(
    string Kind,
    Guid Id,
    string Title,
    Guid? FolderId,
    string? Content,
    WorkspaceListing? Listing);
