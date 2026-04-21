using System.Text.Json.Serialization;

namespace DuckHouse.Core.Workspace;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(Notebook), "Notebook")]
[JsonDerivedType(typeof(Query), "Query")]
public abstract class WorkspaceItem
{
    public Guid Id { get; set; }
    public required string Title { get; set; }
    public Guid? FolderId { get; set; }

    public Folder? Folder { get; set; }

    public required string Content { get; set; }

    public WorkspaceItemType ItemType { get; protected set; }

    /// <summary>
    /// Catalog names associated with this workspace item.
    /// Used to attach DuckLake catalogs to kernel sessions.
    /// </summary>
    public List<string>? CatalogNames { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
