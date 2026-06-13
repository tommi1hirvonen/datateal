using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Datateal.Ui.Shared.Orchestration;
using Datateal.Ui.Shared.Workspaces;

namespace Datateal.Ui.Client.Services;

internal class NodePoolService(HttpClient httpClient, IActiveWorkspaceAccessor workspace) : INodePoolService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private string Ws => $"api/workspaces/{workspace.ActiveWorkspaceId!.Value}/orchestrator/node-pools";

    public async Task<IReadOnlyList<NodePoolConfigDto>> GetNodePoolsAsync(CancellationToken ct)
    {
        return await httpClient.GetFromJsonAsync<IReadOnlyList<NodePoolConfigDto>>(
            Ws, JsonOptions, ct) ?? [];
    }

    public async Task<NodePoolConfigDto> CreateNodePoolAsync(CreateNodePoolRequest request, CancellationToken ct)
    {
        var response = await httpClient.PostAsJsonAsync(Ws, request, JsonOptions, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<NodePoolConfigDto>(JsonOptions, ct))!;
    }

    public async Task<NodePoolConfigDto?> UpdateNodePoolAsync(Guid id, UpdateNodePoolRequest request, CancellationToken ct)
    {
        var response = await httpClient.PutAsJsonAsync($"{Ws}/{id}", request, JsonOptions, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<NodePoolConfigDto>(JsonOptions, ct);
    }

    public async Task DeleteNodePoolAsync(Guid id, CancellationToken ct)
    {
        var response = await httpClient.DeleteAsync($"{Ws}/{id}", ct);
        response.EnsureSuccessStatusCode();
    }
}
