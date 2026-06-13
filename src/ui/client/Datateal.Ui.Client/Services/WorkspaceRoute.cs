namespace Datateal.Ui.Client.Services;

/// <summary>
/// Helpers for the <c>/w/{workspaceId}/...</c> URL convention that encodes the active
/// workspace in the path so links to specific items are shareable.
/// </summary>
public static class WorkspaceRoute
{
    public const string Prefix = "w";

    /// <summary>
    /// Parses a path of the form <c>/w/{guid}/{remainder}</c>. <paramref name="remainder"/>
    /// is the part after the workspace segment (without a leading slash), or empty.
    /// </summary>
    public static bool TryParse(string path, out Guid workspaceId, out string remainder)
    {
        workspaceId = Guid.Empty;
        remainder = "";

        var trimmed = path.TrimStart('/');
        var parts = trimmed.Split('/', 3);
        if (parts.Length >= 2
            && string.Equals(parts[0], Prefix, StringComparison.OrdinalIgnoreCase)
            && Guid.TryParse(parts[1], out workspaceId))
        {
            remainder = parts.Length == 3 ? parts[2] : "";
            return true;
        }

        return false;
    }

    /// <summary>Builds <c>/w/{workspaceId}/{suffix}</c>.</summary>
    public static string Build(Guid workspaceId, string suffix) =>
        $"/{Prefix}/{workspaceId}/{suffix.TrimStart('/')}";
}
