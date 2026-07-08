using System.Text;
using Datateal.Core.Workspace;
using Datateal.Deployment.Diff;
using Datateal.Deployment.Models;
using Datateal.Deployment.Serialization;

namespace Datateal.Ui.Server.Infrastructure.Deployment;

internal sealed class WorkspaceNotebookMapper : IResourceMapper<NotebookModel>
{
    private readonly Dictionary<string, string> _currentContentByPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _desiredContentByPath = new(StringComparer.OrdinalIgnoreCase);

    public string ResourceType => "notebook";

    public void LoadDesired(Bundle bundle)
    {
        _desiredContentByPath.Clear();
        foreach (var model in bundle.Notebooks)
        {
            if (string.IsNullOrWhiteSpace(model.SourceFile) || !bundle.Files.TryGetValue(model.SourceFile, out var bytes))
                continue;

            _desiredContentByPath[NaturalKey(model)] = Encoding.UTF8.GetString(bytes);
        }
    }

    public NotebookModel ToModel(Notebook notebook, string logicalPath)
    {
        var path = DeploymentPathHelpers.NormalizePath(logicalPath);
        _currentContentByPath[path] = notebook.Content;

        return new NotebookModel
        {
            Path = path,
            SourceFile = DeploymentPathHelpers.GetNotebookSourceFile(path),
            Catalogs = NormalizeList(notebook.CatalogNames),
        };
    }

    public string NaturalKey(NotebookModel model) => DeploymentPathHelpers.NormalizePath(model.Path);

    public bool AreEqual(NotebookModel desired, NotebookModel current)
    {
        var key = NaturalKey(desired);
        return string.Equals(key, NaturalKey(current), StringComparison.OrdinalIgnoreCase)
            && NormalizeList(desired.Catalogs).SequenceEqual(NormalizeList(current.Catalogs), StringComparer.OrdinalIgnoreCase)
            && _desiredContentByPath.TryGetValue(key, out var desiredContent)
            && _currentContentByPath.TryGetValue(key, out var currentContent)
            && string.Equals(desiredContent, currentContent, StringComparison.Ordinal);
    }

    private static List<string> NormalizeList(List<string>? values) =>
        (values ?? [])
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
