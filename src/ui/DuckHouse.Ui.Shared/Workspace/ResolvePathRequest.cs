namespace DuckHouse.Ui.Shared.Workspace;

public record ResolvePathRequest(string Path, Guid? BaseFolderId = null);
