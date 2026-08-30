using Datateal.Core.Workspace;

namespace Datateal.Ui.Server.Infrastructure.Deployment;

internal static class DeploymentPathHelpers
{
    public static string NormalizePath(string? path) =>
        (path ?? string.Empty).Replace('\\', '/').Trim('/');

    public static string CombinePath(string? parentPath, string name)
    {
        var parent = NormalizePath(parentPath);
        var child = NormalizePath(name);
        return string.IsNullOrEmpty(parent) ? child : $"{parent}/{child}";
    }

    public static int GetDepth(string? path)
    {
        var normalized = NormalizePath(path);
        return string.IsNullOrEmpty(normalized) ? 0 : normalized.Count(c => c == '/') + 1;
    }

    public static string GetNotebookSourceFile(string logicalPath) =>
        $"src/notebooks/{NormalizePath(logicalPath)}.ipynb";

    public static string GetQuerySourceFile(string logicalPath) =>
        $"src/queries/{NormalizePath(logicalPath)}.sql";

    public static string GetWheelBundleFilePath(string fileName) =>
        $"files/wheels/{fileName}";

    public static IReadOnlyDictionary<Guid, string> BuildFolderPathMap(IEnumerable<Folder> folders)
    {
        var foldersById = folders.ToDictionary(f => f.Id);
        var pathsById = new Dictionary<Guid, string>();

        string BuildPath(Folder folder)
        {
            if (pathsById.TryGetValue(folder.Id, out var cached))
                return cached;

            var path = folder.ParentId is { } parentId && foldersById.TryGetValue(parentId, out var parent)
                ? CombinePath(BuildPath(parent), folder.Name)
                : NormalizePath(folder.Name);

            pathsById[folder.Id] = path;
            return path;
        }

        foreach (var folder in folders)
            BuildPath(folder);

        return pathsById;
    }

    public static string GetItemPath(WorkspaceItem item, IReadOnlyDictionary<Guid, string> folderPaths)
    {
        if (item.FolderId is { } folderId && folderPaths.TryGetValue(folderId, out var folderPath))
            return CombinePath(folderPath, item.Title);

        return NormalizePath(item.Title);
    }
}
