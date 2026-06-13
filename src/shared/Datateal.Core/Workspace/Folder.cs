namespace Datateal.Core.Workspace;

public class Folder
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public Guid? ParentId { get; set; }

    /// <summary>
    /// Owning workspace. All folders belong to exactly one workspace.
    /// </summary>
    public Guid WorkspaceId { get; set; }

    public Folder? Parent { get; set; }
    public List<Folder> Children { get; set; } = [];
    public List<WorkspaceItem> Items { get; set; } = [];

    public DateTime CreatedAt { get; set; }
}
