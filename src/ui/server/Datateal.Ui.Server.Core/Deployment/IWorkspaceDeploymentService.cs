using Datateal.Core.Deployment;
using Datateal.Deployment.Diff;
using Datateal.Deployment.Serialization;

namespace Datateal.Ui.Server.Core.Deployment;

public sealed record WorkspaceDeploymentSnapshot(
    Bundle Bundle,
    Dictionary<string, string> EncryptedSecretsByKey);

public sealed record WorkspaceDeploymentFullSnapshot(
    WorkspaceDeploymentSnapshot UiSnapshot,
    List<Datateal.Deployment.Models.JobModel>? PreviousJobs);

public interface IWorkspaceDeploymentService
{
    Task<ChangeSet> PlanAsync(Guid workspaceId, Bundle bundle, IReadOnlyDictionary<string, string>? env = null, CancellationToken ct = default);
    Task<ChangeSet> ApplyAsync(Guid workspaceId, Bundle bundle, IReadOnlyDictionary<string, string>? env = null, CancellationToken ct = default);
    Task<Bundle> ExportAsync(Guid workspaceId, CancellationToken ct = default);
    Task<WorkspaceDeploymentSnapshot> CreateSnapshotAsync(Guid workspaceId, CancellationToken ct = default);
    Task RestoreSnapshotAsync(Guid workspaceId, WorkspaceDeploymentSnapshot snapshot, CancellationToken ct = default);
    Task<Guid> CreateDeploymentLogAsync(Guid workspaceId, DeploymentScope scope, string targetBundleJson, string snapshotJson, string? issuedByUserId = null, string? issuedByDisplayName = null, CancellationToken ct = default);
    Task UpdateDeploymentLogStatusAsync(Guid logId, DeploymentStatus status, string? failureReason = null, CancellationToken ct = default);
}
