namespace DuckHouse.Core.Workspace;

/// <summary>
/// Lightweight projection of a workspace item used in listing and search queries.
/// Does not include <c>Content</c> — use <see cref="Notebook"/> or <see cref="Query"/> entities
/// when the full content is needed.
/// </summary>
public record WorkspaceItemHeader(
    Guid Id,
    string Title,
    Guid? FolderId,
    WorkspaceItemType ItemType,
    DateTime CreatedAt,
    DateTime UpdatedAt);
