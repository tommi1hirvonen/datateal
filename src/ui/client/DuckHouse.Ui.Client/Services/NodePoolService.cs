using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DuckHouse.Ui.Shared.Orchestration;

namespace DuckHouse.Ui.Client.Services;

internal class NodePoolService(HttpClient httpClient) : INodePoolService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<IReadOnlyList<NodePoolConfigDto>> GetNodePoolsAsync(CancellationToken ct)
    {
        return await httpClient.GetFromJsonAsync<IReadOnlyList<NodePoolConfigDto>>(
            "api/orchestrator/node-pools", JsonOptions, ct) ?? [];
    }

    public async Task<NodePoolConfigDto> CreateNodePoolAsync(CreateNodePoolRequest request, CancellationToken ct)
    {
        var response = await httpClient.PostAsJsonAsync("api/orchestrator/node-pools", request, JsonOptions, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<NodePoolConfigDto>(JsonOptions, ct))!;
    }

    public async Task<NodePoolConfigDto?> UpdateNodePoolAsync(Guid id, UpdateNodePoolRequest request, CancellationToken ct)
    {
        var response = await httpClient.PutAsJsonAsync($"api/orchestrator/node-pools/{id}", request, JsonOptions, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<NodePoolConfigDto>(JsonOptions, ct);
    }

    public async Task DeleteNodePoolAsync(Guid id, CancellationToken ct)
    {
        var response = await httpClient.DeleteAsync($"api/orchestrator/node-pools/{id}", ct);
        response.EnsureSuccessStatusCode();
    }
}
