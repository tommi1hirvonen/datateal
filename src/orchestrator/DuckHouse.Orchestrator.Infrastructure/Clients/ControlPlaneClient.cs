using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DuckHouse.Core.Kernels;
using DuckHouse.Core.Nodes;
using DuckHouse.Orchestrator.Core.Interfaces;

namespace DuckHouse.Orchestrator.Infrastructure.Clients;

internal class ControlPlaneClient(IHttpClientFactory httpClientFactory) : IControlPlaneClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private HttpClient CreateClient() => httpClientFactory.CreateClient("ControlPlane");

    // ── Nodes ───────────────────────────────────────────────────────

    public async Task<NodeInfo> CreateNodeAsync(
        string name,
        string vmSize,
        TimeSpan? kernelIdleTimeout,
        TimeSpan? nodeIdleTimeout,
        string? kernelRequirements,
        IReadOnlyList<WheelContent>? wheelContents,
        IReadOnlyDictionary<string, string>? environmentVariables,
        IReadOnlyDictionary<string, string>? secrets,
        CancellationToken ct)
    {
        using var client = CreateClient();
        var request = new CreateNodeRequest(
            name,
            vmSize,
            kernelIdleTimeout,
            nodeIdleTimeout,
            kernelRequirements,
            wheelContents,
            environmentVariables, secrets);
        var response = await client.PostAsJsonAsync("/nodes", request, JsonOptions, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<NodeInfo>(JsonOptions, ct))!;
    }

    public async Task<IReadOnlyList<NodeInfo>> ListNodesAsync(CancellationToken ct)
    {
        using var client = CreateClient();
        var response = await client.GetAsync("/nodes", ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<NodeInfo>>(JsonOptions, ct))!;
    }

    public async Task<NodeInfo?> GetNodeAsync(string name, CancellationToken ct)
    {
        using var client = CreateClient();
        var response = await client.GetAsync($"/nodes/{Uri.EscapeDataString(name)}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<NodeInfo>(JsonOptions, ct);
    }

    public async Task DeleteNodeAsync(string name, CancellationToken ct)
    {
        using var client = CreateClient();
        var response = await client.DeleteAsync($"/nodes/{Uri.EscapeDataString(name)}", ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateNodeEvictionConfigAsync(string name, TimeSpan? kernelIdleTimeout, TimeSpan? nodeIdleTimeout, CancellationToken ct)
    {
        using var client = CreateClient();
        var request = new { KernelIdleTimeout = kernelIdleTimeout, NodeIdleTimeout = nodeIdleTimeout };
        var response = await client.PutAsJsonAsync($"/nodes/{Uri.EscapeDataString(name)}/config", request, JsonOptions, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return;
        response.EnsureSuccessStatusCode();
    }

    // ── Kernels ─────────────────────────────────────────────────────

    public async Task<KernelInfo> CreateKernelAsync(string nodeName, CancellationToken ct)
    {
        using var client = CreateClient();
        var response = await client.PostAsync(
            $"/nodes/{Uri.EscapeDataString(nodeName)}/kernels", null, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<KernelInfo>(JsonOptions, ct))!;
    }

    public async Task DeleteKernelAsync(string nodeName, string kernelId, CancellationToken ct)
    {
        using var client = CreateClient();
        var response = await client.DeleteAsync(
            $"/nodes/{Uri.EscapeDataString(nodeName)}/kernels/{Uri.EscapeDataString(kernelId)}", ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<KernelInfo> RestartKernelAsync(string nodeName, string kernelId, CancellationToken ct)
    {
        using var client = CreateClient();
        var response = await client.PostAsync(
            $"/nodes/{Uri.EscapeDataString(nodeName)}/kernels/{Uri.EscapeDataString(kernelId)}/restart",
            null, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<KernelInfo>(JsonOptions, ct))!;
    }

    public async Task InterruptKernelAsync(string nodeName, string kernelId, CancellationToken ct)
    {
        using var client = CreateClient();
        var response = await client.PostAsync(
            $"/nodes/{Uri.EscapeDataString(nodeName)}/kernels/{Uri.EscapeDataString(kernelId)}/interrupt",
            null, ct);
        response.EnsureSuccessStatusCode();
    }

    // ── Execution ───────────────────────────────────────────────────

    public async Task<ExecutionHandle> StartExecuteAsync(string nodeName, string kernelId,
        string code, double? timeout, CancellationToken ct)
    {
        using var client = CreateClient();
        var request = new ExecuteRequest(code, timeout);
        var response = await client.PostAsJsonAsync(
            $"/nodes/{Uri.EscapeDataString(nodeName)}/kernels/{Uri.EscapeDataString(kernelId)}/execute",
            request, JsonOptions, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ExecutionHandle>(JsonOptions, ct))!;
    }

    public async Task<PollExecutionResponse> PollExecutionAsync(string nodeName, string kernelId,
        string executionId, CancellationToken ct)
    {
        using var client = CreateClient();
        var response = await client.GetAsync(
            $"/nodes/{Uri.EscapeDataString(nodeName)}/kernels/{Uri.EscapeDataString(kernelId)}/executions/{Uri.EscapeDataString(executionId)}",
            ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<PollExecutionResponse>(JsonOptions, ct))!;
    }
}
