namespace DuckHouse.Ui.Server.Core.Workspace;

/// <summary>
/// Thrown when a notebook or query title is not unique within the target folder.
/// </summary>
public sealed class WorkspaceTitleConflictException(string title, string folderDescription)
    : InvalidOperationException(
        $"A notebook or query named \"{title}\" already exists in {folderDescription}.")
{
    public string Title { get; } = title;
}
