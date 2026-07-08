using Datateal.Ui.Shared.Deployment;

namespace Datateal.Ui.Client.Services;

public interface IDeploymentService
{
    Task<ChangeSetDto> PlanAdminDeploymentAsync(Stream bundleZip, CancellationToken ct = default);
    Task<ChangeSetDto> ApplyAdminDeploymentAsync(Stream bundleZip, CancellationToken ct = default);
    Task<byte[]> ExportAdminBundleAsync(CancellationToken ct = default);
    Task<ChangeSetDto> PlanWorkspaceDeploymentAsync(Guid workspaceId, Stream bundleZip, CancellationToken ct = default);
    Task<ChangeSetDto> ApplyWorkspaceDeploymentAsync(Guid workspaceId, Stream bundleZip, CancellationToken ct = default);
    Task<byte[]> ExportWorkspaceBundleAsync(Guid workspaceId, CancellationToken ct = default);
}
