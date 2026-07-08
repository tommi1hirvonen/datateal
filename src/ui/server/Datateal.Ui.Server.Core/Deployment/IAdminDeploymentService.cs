using Datateal.Deployment.Diff;
using Datateal.Deployment.Serialization;

namespace Datateal.Ui.Server.Core.Deployment;

public interface IAdminDeploymentService
{
    Task<ChangeSet> PlanAsync(Bundle bundle, IReadOnlyDictionary<string, string>? env = null, CancellationToken ct = default);
    Task<ChangeSet> ApplyAsync(Bundle bundle, IReadOnlyDictionary<string, string>? env = null, CancellationToken ct = default);
    Task<Bundle> ExportAsync(CancellationToken ct = default);
}
