using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DuckHouse.Core.Kernels;
using DuckHouse.Ui.Server.Core.Repositories;

namespace DuckHouse.Ui.Server.Infrastructure.Repositories;

internal class KernelRepository(HttpClient httpClient) : IKernelRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<IReadOnlyList<KernelInfo>> GetKernelsAsync(string nodeName, CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<List<KernelInfo>>($"/nodes/{nodeName}/kernels", JsonOptions, cancellationToken) ?? [];

    public async Task<KernelInfo> CreateKernelAsync(string nodeName, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsync($"/nodes/{nodeName}/kernels", content: null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<KernelInfo>(JsonOptions, cancellationToken))!;
    }

    public async Task<KernelInfo> GetKernelAsync(string nodeName, string kernelId, CancellationToken cancellationToken = default) =>
        (await httpClient.GetFromJsonAsync<KernelInfo>($"/nodes/{nodeName}/kernels/{kernelId}", JsonOptions, cancellationToken))!;

    public async Task DeleteKernelAsync(string nodeName, string kernelId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync($"/nodes/{nodeName}/kernels/{kernelId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<ExecutionHandle> StartExecuteAsync(string nodeName, string kernelId, ExecuteRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync($"/nodes/{nodeName}/kernels/{kernelId}/execute", request, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ExecutionHandle>(JsonOptions, cancellationToken))!;
    }

    public async Task<PollExecutionResponse> PollExecutionAsync(string nodeName, string kernelId, string executionId, CancellationToken cancellationToken = default) =>
        (await httpClient.GetFromJsonAsync<PollExecutionResponse>($"/nodes/{nodeName}/kernels/{kernelId}/executions/{executionId}", JsonOptions, cancellationToken))!;

    public async Task<KernelInfo> RestartKernelAsync(string nodeName, string kernelId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsync($"/nodes/{nodeName}/kernels/{kernelId}/restart", content: null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<KernelInfo>(JsonOptions, cancellationToken))!;
    }

    public async Task InterruptKernelAsync(string nodeName, string kernelId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsync($"/nodes/{nodeName}/kernels/{kernelId}/interrupt", content: null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<CompleteResponse> CompleteAsync(string nodeName, string kernelId, CompleteRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync($"/nodes/{nodeName}/kernels/{kernelId}/completions", request, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CompleteResponse>(JsonOptions, cancellationToken))!;
    }

    public async Task<DiagnoseResponse> DiagnoseAsync(string nodeName, string kernelId, DiagnoseRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync($"/nodes/{nodeName}/kernels/{kernelId}/diagnostics", request, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DiagnoseResponse>(JsonOptions, cancellationToken))!;
    }
}
