using Datateal.Core.Deployment;
using Datateal.Deployment.Diff;
using Datateal.Deployment.Serialization;

namespace Datateal.Ui.Server.Core.Deployment;

public interface IAdminDeploymentService
{
    Task<ChangeSet> PlanAsync(Bundle bundle, IReadOnlyDictionary<string, string>? env = null, CancellationToken ct = default);
    Task<ChangeSet> ApplyAsync(Bundle bundle, IReadOnlyDictionary<string, string>? env = null, CancellationToken ct = default);
    Task<Bundle> ExportAsync(CancellationToken ct = default);

    /// <summary>
    /// Creates a <see cref="DeploymentScope.Admin"/> audit log entry for an apply. Unlike
    /// workspace deployments, admin applies are audited (who/what/when) but not sagas: an admin
    /// apply is a single atomic database transaction, so there is nothing to roll back if it
    /// fails partway — the transaction itself is never committed. <paramref name="snapshotJson"/>
    /// is retained purely for troubleshooting/audit value (e.g. "what did admin state look like
    /// right before this apply"), not for restoration.
    /// </summary>
    Task<Guid> CreateDeploymentLogAsync(string targetBundleJson, string snapshotJson, string? issuedByUserId = null, string? issuedByDisplayName = null, CancellationToken ct = default);

    Task UpdateDeploymentLogStatusAsync(Guid logId, DeploymentStatus status, string? failureReason = null, CancellationToken ct = default);
}
