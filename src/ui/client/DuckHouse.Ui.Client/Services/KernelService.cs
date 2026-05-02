using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DuckHouse.Core.Kernels;
using DuckHouse.Ui.Shared.Kernels;

namespace DuckHouse.Ui.Client.Services;

internal class KernelService(HttpClient httpClient) : IKernelService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<IReadOnlyList<KernelInfo>> GetKernelsAsync(string nodeName, CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<List<KernelInfo>>($"api/nodes/{nodeName}/kernels", JsonOptions, cancellationToken) ?? [];

    public async Task<KernelInfo> CreateKernelAsync(string nodeName, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsync($"api/nodes/{nodeName}/kernels", content: null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<KernelInfo>(JsonOptions, cancellationToken))!;
    }

    public async Task<KernelInfo> GetKernelAsync(string nodeName, string kernelId, CancellationToken cancellationToken = default) =>
        (await httpClient.GetFromJsonAsync<KernelInfo>($"api/nodes/{nodeName}/kernels/{kernelId}", JsonOptions, cancellationToken))!;

    public async Task DeleteKernelAsync(string nodeName, string kernelId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync($"api/nodes/{nodeName}/kernels/{kernelId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<ExecutionHandle> StartExecuteAsync(string nodeName, string kernelId, ExecuteKernelRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync($"api/nodes/{nodeName}/kernels/{kernelId}/execute", request, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ExecutionHandle>(JsonOptions, cancellationToken))!;
    }

    public async Task<PollExecutionResponse> PollExecutionAsync(string nodeName, string kernelId, string executionId, CancellationToken cancellationToken = default) =>
        (await httpClient.GetFromJsonAsync<PollExecutionResponse>($"api/nodes/{nodeName}/kernels/{kernelId}/executions/{executionId}", JsonOptions, cancellationToken))!;

    public async Task<KernelInfo> RestartKernelAsync(string nodeName, string kernelId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsync($"api/nodes/{nodeName}/kernels/{kernelId}/restart", content: null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<KernelInfo>(JsonOptions, cancellationToken))!;
    }

    public async Task InterruptKernelAsync(string nodeName, string kernelId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsync($"api/nodes/{nodeName}/kernels/{kernelId}/interrupt", content: null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<CompleteResponse> CompleteAsync(string nodeName, string kernelId, CompleteRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync($"api/nodes/{nodeName}/kernels/{kernelId}/completions", request, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CompleteResponse>(JsonOptions, cancellationToken))!;
    }

    public async Task<DiagnoseResponse> DiagnoseAsync(string nodeName, string kernelId, DiagnoseRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync($"api/nodes/{nodeName}/kernels/{kernelId}/diagnostics", request, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DiagnoseResponse>(JsonOptions, cancellationToken))!;
    }

    public async Task<SemanticTokenResponse> GetSemanticTokensAsync(string nodeName, string kernelId, SemanticTokenRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync($"api/nodes/{nodeName}/kernels/{kernelId}/semantic-tokens", request, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SemanticTokenResponse>(JsonOptions, cancellationToken))!;
    }

    public async Task<HoverInfoResponse> GetHoverInfoAsync(string nodeName, string kernelId, HoverInfoRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync($"api/nodes/{nodeName}/kernels/{kernelId}/hover", request, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<HoverInfoResponse>(JsonOptions, cancellationToken))!;
    }
}
