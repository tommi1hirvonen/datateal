using System.Net.Http.Headers;
using System.Net.Http.Json;
using Datateal.Ui.Shared.Deployment;

namespace Datateal.Ui.Client.Services;

internal sealed class DeploymentService(HttpClient httpClient) : IDeploymentService
{
    public Task<ChangeSetDto> PlanAdminDeploymentAsync(Stream bundleZip, CancellationToken ct = default) =>
        SendBundleAsync("api/deployments/admin/plan", bundleZip, ct);

    public Task<ChangeSetDto> ApplyAdminDeploymentAsync(Stream bundleZip, CancellationToken ct = default) =>
        SendBundleAsync("api/deployments/admin/apply", bundleZip, ct);

    public Task<byte[]> ExportAdminBundleAsync(CancellationToken ct = default) =>
        GetBytesAsync("api/deployments/admin/export", ct);

    public Task<ChangeSetDto> PlanWorkspaceDeploymentAsync(Guid workspaceId, Stream bundleZip, CancellationToken ct = default) =>
        SendBundleAsync($"api/workspaces/{workspaceId}/deployment/plan", bundleZip, ct);

    public Task<ChangeSetDto> ApplyWorkspaceDeploymentAsync(Guid workspaceId, Stream bundleZip, CancellationToken ct = default) =>
        SendBundleAsync($"api/workspaces/{workspaceId}/deployment/apply", bundleZip, ct);

    public Task<byte[]> ExportWorkspaceBundleAsync(Guid workspaceId, CancellationToken ct = default) =>
        GetBytesAsync($"api/workspaces/{workspaceId}/deployment/export", ct);

    private async Task<ChangeSetDto> SendBundleAsync(string uri, Stream bundleZip, CancellationToken ct)
    {
        using var content = new StreamContent(bundleZip);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        using var response = await httpClient.PostAsync(uri, content, ct);
        await response.EnsureSuccessWithDetailsAsync(ct);
        return (await response.Content.ReadFromJsonAsync<ChangeSetDto>(ct))!;
    }

    private async Task<byte[]> GetBytesAsync(string uri, CancellationToken ct)
    {
        using var response = await httpClient.GetAsync(uri, ct);
        await response.EnsureSuccessWithDetailsAsync(ct);
        return await response.Content.ReadAsByteArrayAsync(ct);
    }
}
