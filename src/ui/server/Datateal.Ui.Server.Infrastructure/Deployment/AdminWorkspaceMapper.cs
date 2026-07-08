using Datateal.Core.Workspaces;
using Datateal.Deployment.Diff;
using Datateal.Deployment.Models;

namespace Datateal.Ui.Server.Infrastructure.Deployment;

internal sealed class AdminWorkspaceMapper : IResourceMapper<WorkspaceModel>
{
    public string ResourceType => "workspace";

    public WorkspaceModel ToModel(Workspace workspace) => new()
    {
        Name = workspace.Name,
        Description = workspace.Description,
    };

    public string NaturalKey(WorkspaceModel model) => model.Name.Trim();

    public bool AreEqual(WorkspaceModel desired, WorkspaceModel current) =>
        string.Equals(NaturalKey(desired), NaturalKey(current), StringComparison.OrdinalIgnoreCase)
        && string.Equals(desired.Description, current.Description, StringComparison.Ordinal);
}
