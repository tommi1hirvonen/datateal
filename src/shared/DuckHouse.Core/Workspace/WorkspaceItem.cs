namespace DuckHouse.Core.Workspace;

public abstract class WorkspaceItem
{
    public Guid Id { get; set; }
    public required string Title { get; set; }
    public Guid? FolderId { get; set; }

    public Folder? Folder { get; set; }

    public required string Content { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
