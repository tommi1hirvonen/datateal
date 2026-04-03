using System.Text.Json;
using k8s;

namespace DuckHouse.ControlPlane.Api.Nodes.Kernels;

// Forwards runtime API calls to a pod via the Kubernetes API server HTTP proxy.
// The API server authenticates and tunnels the request to the pod without requiring
// a Kubernetes Service, public IP, or VNet-level access from the caller.
// Proxy URL format: /api/v1/namespaces/{ns}/pods/{pod}:{port}/proxy/{path}
public sealed class KubernetesRuntimeClient : INodeRuntimeClient
{
    private const string Namespace = "default";
    private const int RuntimePort = 8000;

    private readonly HttpClient _httpClient;
    private readonly Uri _baseUri;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public KubernetesRuntimeClient(Kubernetes kubernetes)
    {
        _httpClient = kubernetes.HttpClient;
        _baseUri = kubernetes.BaseUri;
    }

    private Uri ProxyUri(string nodeName, string path) =>
        new(_baseUri, $"api/v1/namespaces/{Namespace}/pods/{nodeName}:{RuntimePort}/proxy/{path}");

    public async Task<IReadOnlyList<KernelInfo>> ListKernelsAsync(string nodeName, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(ProxyUri(nodeName, "kernels"), cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<KernelInfo>>(_jsonOptions, cancellationToken) ?? [];
    }

    public async Task<KernelInfo> CreateKernelAsync(string nodeName, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync(ProxyUri(nodeName, "kernels"), content: null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<KernelInfo>(_jsonOptions, cancellationToken))!;
    }

    public async Task<KernelInfo> GetKernelAsync(string nodeName, string kernelId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(ProxyUri(nodeName, $"kernels/{kernelId}"), cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<KernelInfo>(_jsonOptions, cancellationToken))!;
    }

    public async Task DeleteKernelAsync(string nodeName, string kernelId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync(ProxyUri(nodeName, $"kernels/{kernelId}"), cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<ExecutionResult> ExecuteAsync(string nodeName, string kernelId, ExecuteRequest request, CancellationToken cancellationToken = default)
    {
        var content = JsonContent.Create(request, options: _jsonOptions);
        var response = await _httpClient.PostAsync(ProxyUri(nodeName, $"kernels/{kernelId}/execute"), content, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ExecutionResult>(_jsonOptions, cancellationToken))!;
    }

    public async Task<KernelInfo> RestartKernelAsync(string nodeName, string kernelId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync(ProxyUri(nodeName, $"kernels/{kernelId}/restart"), content: null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<KernelInfo>(_jsonOptions, cancellationToken))!;
    }

    public async Task InterruptKernelAsync(string nodeName, string kernelId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync(ProxyUri(nodeName, $"kernels/{kernelId}/interrupt"), content: null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
