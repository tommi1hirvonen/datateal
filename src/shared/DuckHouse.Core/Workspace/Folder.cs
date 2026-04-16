namespace DuckHouse.Core.Workspace;

public class Folder
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public Guid? ParentId { get; set; }

    public Folder? Parent { get; set; }
    public List<Folder> Children { get; set; } = [];
    public List<WorkspaceItem> Items { get; set; } = [];

    public DateTime CreatedAt { get; set; }
}
