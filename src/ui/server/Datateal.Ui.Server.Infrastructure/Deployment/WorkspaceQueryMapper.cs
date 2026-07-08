using System.Text;
using Datateal.Core.Workspace;
using Datateal.Deployment.Diff;
using Datateal.Deployment.Models;
using Datateal.Deployment.Serialization;

namespace Datateal.Ui.Server.Infrastructure.Deployment;

internal sealed class WorkspaceQueryMapper : IResourceMapper<QueryModel>
{
    private readonly Dictionary<string, string> _currentContentByPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _desiredContentByPath = new(StringComparer.OrdinalIgnoreCase);

    public string ResourceType => "query";

    public void LoadDesired(Bundle bundle)
    {
        _desiredContentByPath.Clear();
        foreach (var model in bundle.Queries)
        {
            if (string.IsNullOrWhiteSpace(model.SourceFile) || !bundle.Files.TryGetValue(model.SourceFile, out var bytes))
                continue;

            _desiredContentByPath[NaturalKey(model)] = Encoding.UTF8.GetString(bytes);
        }
    }

    public QueryModel ToModel(Query query, string logicalPath)
    {
        var path = DeploymentPathHelpers.NormalizePath(logicalPath);
        _currentContentByPath[path] = query.Content;

        return new QueryModel
        {
            Path = path,
            SourceFile = DeploymentPathHelpers.GetQuerySourceFile(path),
            Catalogs = NormalizeList(query.CatalogNames),
        };
    }

    public string NaturalKey(QueryModel model) => DeploymentPathHelpers.NormalizePath(model.Path);

    public bool AreEqual(QueryModel desired, QueryModel current)
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
