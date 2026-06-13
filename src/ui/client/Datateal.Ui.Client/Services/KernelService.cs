using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Datateal.Core.Kernels;
using Datateal.Ui.Shared.Kernels;
using Datateal.Ui.Shared.Workspaces;

namespace Datateal.Ui.Client.Services;

internal class KernelService(HttpClient httpClient, IActiveWorkspaceAccessor workspace) : IKernelService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private string KernelsBase(string nodeName) =>
        $"api/workspaces/{workspace.ActiveWorkspaceId!.Value}/nodes/{nodeName}/kernels";

    public async Task<IReadOnlyList<KernelInfo>> GetKernelsAsync(string nodeName, CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<List<KernelInfo>>(KernelsBase(nodeName), JsonOptions, cancellationToken) ?? [];

    public async Task<KernelInfo> CreateKernelAsync(string nodeName, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsync(KernelsBase(nodeName), content: null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<KernelInfo>(JsonOptions, cancellationToken))!;
    }

    public async Task<KernelInfo> GetKernelAsync(string nodeName, string kernelId, CancellationToken cancellationToken = default) =>
        (await httpClient.GetFromJsonAsync<KernelInfo>($"{KernelsBase(nodeName)}/{kernelId}", JsonOptions, cancellationToken))!;

    public async Task DeleteKernelAsync(string nodeName, string kernelId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync($"{KernelsBase(nodeName)}/{kernelId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<ExecutionHandle> StartExecuteAsync(string nodeName, string kernelId, ExecuteKernelRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync($"{KernelsBase(nodeName)}/{kernelId}/execute", request, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ExecutionHandle>(JsonOptions, cancellationToken))!;
    }

    public async Task<PollExecutionResponse> PollExecutionAsync(string nodeName, string kernelId, string executionId, CancellationToken cancellationToken = default) =>
        (await httpClient.GetFromJsonAsync<PollExecutionResponse>($"{KernelsBase(nodeName)}/{kernelId}/executions/{executionId}", JsonOptions, cancellationToken))!;

    public async Task<KernelInfo> RestartKernelAsync(string nodeName, string kernelId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsync($"{KernelsBase(nodeName)}/{kernelId}/restart", content: null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<KernelInfo>(JsonOptions, cancellationToken))!;
    }

    public async Task InterruptKernelAsync(string nodeName, string kernelId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsync($"{KernelsBase(nodeName)}/{kernelId}/interrupt", content: null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<CompleteResponse> CompleteAsync(string nodeName, string kernelId, CompleteRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync($"{KernelsBase(nodeName)}/{kernelId}/completions", request, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CompleteResponse>(JsonOptions, cancellationToken))!;
    }

    public async Task<DiagnoseResponse> DiagnoseAsync(string nodeName, string kernelId, DiagnoseRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync($"{KernelsBase(nodeName)}/{kernelId}/diagnostics", request, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DiagnoseResponse>(JsonOptions, cancellationToken))!;
    }

    public async Task<SemanticTokenResponse> GetSemanticTokensAsync(string nodeName, string kernelId, SemanticTokenRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync($"{KernelsBase(nodeName)}/{kernelId}/semantic-tokens", request, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SemanticTokenResponse>(JsonOptions, cancellationToken))!;
    }

    public async Task<HoverInfoResponse> GetHoverInfoAsync(string nodeName, string kernelId, HoverInfoRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync($"{KernelsBase(nodeName)}/{kernelId}/hover", request, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<HoverInfoResponse>(JsonOptions, cancellationToken))!;
    }
}
