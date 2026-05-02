using System.Net.Http.Json;
using System.Text.Json;
using DuckHouse.ControlPlane.Core.Services;
using DuckHouse.Core.Kernels;
using k8s;
using k8s.Authentication;

namespace DuckHouse.ControlPlane.Infrastructure.Nodes.Kernels;

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
    private readonly ITokenProvider? _tokenProvider;
    private readonly string _runtimeApiKey;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    public KubernetesRuntimeClient(Kubernetes kubernetes, ITokenProvider? tokenProvider = null, string? runtimeApiKey = null)
    {
        _httpClient = kubernetes.HttpClient;
        _baseUri = kubernetes.BaseUri;
        _tokenProvider = tokenProvider;
        _runtimeApiKey = runtimeApiKey ?? "";
    }

    private Uri ProxyUri(string nodeName, string path) =>
        new(_baseUri, $"api/v1/namespaces/{Namespace}/pods/{nodeName}:{RuntimePort}/proxy/{path}");

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_tokenProvider is not null)
            request.Headers.Authorization = await _tokenProvider.GetAuthenticationHeaderAsync(cancellationToken);
        if (!string.IsNullOrEmpty(_runtimeApiKey))
            request.Headers.Add("X-Api-Key", _runtimeApiKey);
        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return response;
    }

    public async Task<IReadOnlyList<KernelInfo>> ListKernelsAsync(string nodeName, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ProxyUri(nodeName, "kernels"));
        var response = await SendAsync(request, cancellationToken);
        return await response.Content.ReadFromJsonAsync<List<KernelInfo>>(_jsonOptions, cancellationToken) ?? [];
    }

    public async Task<KernelInfo> CreateKernelAsync(string nodeName, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, ProxyUri(nodeName, "kernels"));
        var response = await SendAsync(request, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<KernelInfo>(_jsonOptions, cancellationToken))!;
    }

    public async Task<KernelInfo> GetKernelAsync(string nodeName, string kernelId, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ProxyUri(nodeName, $"kernels/{kernelId}"));
        var response = await SendAsync(request, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<KernelInfo>(_jsonOptions, cancellationToken))!;
    }

    public async Task DeleteKernelAsync(string nodeName, string kernelId, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, ProxyUri(nodeName, $"kernels/{kernelId}"));
        await SendAsync(request, cancellationToken);
    }

    public async Task<ExecutionHandle> StartExecuteAsync(string nodeName, string kernelId, ExecuteRequest request, CancellationToken cancellationToken = default)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, ProxyUri(nodeName, $"kernels/{kernelId}/execute"))
        {
            Content = JsonContent.Create(request, options: _jsonOptions),
        };
        var response = await SendAsync(httpRequest, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<ExecutionHandle>(_jsonOptions, cancellationToken))!;
    }

    public async Task<PollExecutionResponse> PollExecutionAsync(string nodeName, string kernelId, string executionId, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ProxyUri(nodeName, $"kernels/{kernelId}/executions/{executionId}"));
        var response = await SendAsync(request, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<PollExecutionResponse>(_jsonOptions, cancellationToken))!;
    }

    public async Task<KernelInfo> RestartKernelAsync(string nodeName, string kernelId, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, ProxyUri(nodeName, $"kernels/{kernelId}/restart"));
        var response = await SendAsync(request, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<KernelInfo>(_jsonOptions, cancellationToken))!;
    }

    public async Task InterruptKernelAsync(string nodeName, string kernelId, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, ProxyUri(nodeName, $"kernels/{kernelId}/interrupt"));
        await SendAsync(request, cancellationToken);
    }

    public async Task<CompleteResponse> CompleteAsync(string nodeName, string kernelId, CompleteRequest request, CancellationToken cancellationToken = default)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, ProxyUri(nodeName, $"kernels/{kernelId}/completions"))
        {
            Content = JsonContent.Create(request, options: _jsonOptions),
        };
        var response = await SendAsync(httpRequest, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<CompleteResponse>(_jsonOptions, cancellationToken))!;
    }

    public async Task<DiagnoseResponse> DiagnoseAsync(string nodeName, string kernelId, DiagnoseRequest request, CancellationToken cancellationToken = default)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, ProxyUri(nodeName, $"kernels/{kernelId}/diagnostics"))
        {
            Content = JsonContent.Create(request, options: _jsonOptions),
        };
        var response = await SendAsync(httpRequest, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<DiagnoseResponse>(_jsonOptions, cancellationToken))!;
    }

    public async Task<SemanticTokenResponse> GetSemanticTokensAsync(string nodeName, string kernelId, SemanticTokenRequest request, CancellationToken cancellationToken = default)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, ProxyUri(nodeName, $"kernels/{kernelId}/semantic-tokens"))
        {
            Content = JsonContent.Create(request, options: _jsonOptions),
        };
        var response = await SendAsync(httpRequest, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<SemanticTokenResponse>(_jsonOptions, cancellationToken))!;
    }

    public async Task<HoverInfoResponse> HoverAsync(string nodeName, string kernelId, HoverInfoRequest request, CancellationToken cancellationToken = default)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, ProxyUri(nodeName, $"kernels/{kernelId}/hover"))
        {
            Content = JsonContent.Create(request, options: _jsonOptions),
        };
        var httpResponse = await SendAsync(httpRequest, cancellationToken);
        var response = await httpResponse.Content.ReadFromJsonAsync<HoverInfoResponse>(_jsonOptions, cancellationToken);
        return response!;
    }
}
