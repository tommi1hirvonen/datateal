using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Datateal.Core.Nodes;
using Datateal.Ui.Shared.Nodes;
using Datateal.Ui.Shared.Workspaces;

namespace Datateal.Ui.Client.Services;

internal class InteractivePoolService(HttpClient httpClient, IActiveWorkspaceAccessor workspace) : IInteractivePoolService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private string Ws => $"api/workspaces/{workspace.ActiveWorkspaceId!.Value}/interactive-pools";

    public async Task<IReadOnlyList<InteractivePoolDto>> GetInteractivePoolsAsync(
        CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<List<InteractivePoolDto>>(
            Ws, JsonOptions, cancellationToken) ?? [];

    public async Task<NodeInfo?> EnsureNodeAsync(string poolName, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsync(
            $"{Ws}/{Uri.EscapeDataString(poolName)}/ensure-node",
            content: null, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<NodeInfo>(JsonOptions, cancellationToken);
    }
}
