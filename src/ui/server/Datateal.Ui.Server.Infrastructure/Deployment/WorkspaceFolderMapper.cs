using Datateal.Core.Workspace;
using Datateal.Deployment.Diff;
using Datateal.Deployment.Models;

namespace Datateal.Ui.Server.Infrastructure.Deployment;

internal sealed class WorkspaceFolderMapper : IResourceMapper<FolderModel>
{
    public string ResourceType => "folder";

    public string NaturalKey(FolderModel model) => DeploymentPathHelpers.NormalizePath(model.Path);

    public bool AreEqual(FolderModel desired, FolderModel current) =>
        string.Equals(NaturalKey(desired), NaturalKey(current), StringComparison.OrdinalIgnoreCase);

    public FolderModel ToModel(Folder folder, string path) => new()
    {
        Path = DeploymentPathHelpers.NormalizePath(path),
    };
}
