using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Datateal.Core.Nodes;
using Datateal.Ui.Shared.Workspaces;

namespace Datateal.Ui.Client.Services;

internal class NodeService(HttpClient httpClient, IActiveWorkspaceAccessor workspace) : INodeService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private string Ws => $"api/workspaces/{workspace.ActiveWorkspaceId!.Value}/nodes";

    public async Task<IReadOnlyList<NodeInfo>> GetNodesAsync(CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<List<NodeInfo>>(Ws, JsonOptions, cancellationToken) ?? [];

    public async Task<NodeInfo?> GetNodeAsync(string name, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"{Ws}/{name}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<NodeInfo>(JsonOptions, cancellationToken);
    }

    public async Task RemoveNodeAsync(string name, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync($"{Ws}/{name}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
