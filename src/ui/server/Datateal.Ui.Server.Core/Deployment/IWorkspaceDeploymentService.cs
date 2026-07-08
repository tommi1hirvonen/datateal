using Datateal.Deployment.Diff;
using Datateal.Deployment.Serialization;

namespace Datateal.Ui.Server.Core.Deployment;

public interface IWorkspaceDeploymentService
{
    Task<ChangeSet> PlanAsync(Guid workspaceId, Bundle bundle, CancellationToken ct = default);
    Task<ChangeSet> ApplyAsync(Guid workspaceId, Bundle bundle, CancellationToken ct = default);
    Task<Bundle> ExportAsync(Guid workspaceId, CancellationToken ct = default);
}
