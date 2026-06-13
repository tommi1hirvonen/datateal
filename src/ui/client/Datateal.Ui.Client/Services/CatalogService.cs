using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Datateal.Core.Kernels;
using Datateal.Ui.Shared.Catalogs;
using Datateal.Ui.Shared.Workspaces;

namespace Datateal.Ui.Client.Services;

internal class CatalogService(HttpClient httpClient, IActiveWorkspaceAccessor workspace) : ICatalogService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private string Ws => $"api/workspaces/{workspace.ActiveWorkspaceId!.Value}";
    private string WithWorkspaceQuery(string path) =>
        workspace.ActiveWorkspaceId is Guid workspaceId
            ? $"{path}?workspaceId={workspaceId}"
            : path;

    public async Task<IReadOnlyList<CatalogDto>> GetCatalogsAsync(CancellationToken ct = default) =>
        await httpClient.GetFromJsonAsync<IReadOnlyList<CatalogDto>>(WithWorkspaceQuery("api/catalogs"), JsonOptions, ct) ?? [];

    public async Task<IReadOnlyList<CatalogDto>> GetAllCatalogsAsync(CancellationToken ct = default) =>
        await httpClient.GetFromJsonAsync<IReadOnlyList<CatalogDto>>("api/catalogs/all", JsonOptions, ct) ?? [];

    public async Task<CatalogWorkspaceAccessDto> GetWorkspaceAccessAsync(Guid catalogId, CancellationToken ct = default) =>
        await httpClient.GetFromJsonAsync<CatalogWorkspaceAccessDto>($"api/catalogs/{catalogId}/workspace-access", JsonOptions, ct)
            ?? new CatalogWorkspaceAccessDto(true, []);

    public async Task SetWorkspaceAccessAsync(Guid catalogId, SetCatalogWorkspaceAccessRequest request, CancellationToken ct = default)
    {
        var response = await httpClient.PutAsJsonAsync($"api/catalogs/{catalogId}/workspace-access", request, JsonOptions, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<ManagedCatalogDto> CreateManagedCatalogAsync(CreateManagedCatalogRequest request, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsJsonAsync("api/catalogs/managed", request, JsonOptions, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ManagedCatalogDto>(JsonOptions, ct))!;
    }

    public async Task<UnmanagedCatalogDto> CreateUnmanagedCatalogAsync(CreateUnmanagedCatalogRequest request, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsJsonAsync("api/catalogs/unmanaged", request, JsonOptions, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<UnmanagedCatalogDto>(JsonOptions, ct))!;
    }

    public async Task<ManagedCatalogDto?> UpdateManagedCatalogAsync(Guid id, UpdateManagedCatalogRequest request, CancellationToken ct = default)
    {
        var response = await httpClient.PutAsJsonAsync($"api/catalogs/{id}/managed", request, JsonOptions, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ManagedCatalogDto>(JsonOptions, ct);
    }

    public async Task<UnmanagedCatalogDto?> UpdateUnmanagedCatalogAsync(Guid id, UpdateUnmanagedCatalogRequest request, CancellationToken ct = default)
    {
        var response = await httpClient.PutAsJsonAsync($"api/catalogs/{id}/unmanaged", request, JsonOptions, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UnmanagedCatalogDto>(JsonOptions, ct);
    }

    public async Task DeleteCatalogAsync(Guid id, CancellationToken ct = default)
    {
        var response = await httpClient.DeleteAsync($"api/catalogs/{id}", ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<CatalogMetadataDto> GetMetadataAsync(Guid catalogId, CancellationToken ct = default) =>
        await httpClient.GetFromJsonAsync<CatalogMetadataDto>(WithWorkspaceQuery($"api/catalogs/{catalogId}/metadata"), JsonOptions, ct)
        ?? new CatalogMetadataDto("", []);

    public async Task<CatalogInfoDto> GetCatalogInfoAsync(Guid catalogId, CancellationToken ct = default) =>
        await httpClient.GetFromJsonAsync<CatalogInfoDto>(WithWorkspaceQuery($"api/catalogs/{catalogId}/info"), JsonOptions, ct)
        ?? new CatalogInfoDto([]);

    public async Task<ExecutionHandle> SetupCatalogsOnKernelAsync(string nodeName, string kernelId, List<string> catalogNames, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsJsonAsync(
            $"{Ws}/nodes/{nodeName}/kernels/{kernelId}/catalogs/setup",
            new KernelCatalogSetupRequest(catalogNames), JsonOptions, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ExecutionHandle>(JsonOptions, ct))!;
    }

    public async Task<ExecutionHandle> ConnectCatalogOnKernelAsync(string nodeName, string kernelId, string catalogName, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsync(
            $"{Ws}/nodes/{nodeName}/kernels/{kernelId}/catalogs/{Uri.EscapeDataString(catalogName)}/connect", null, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ExecutionHandle>(JsonOptions, ct))!;
    }

    public async Task<ExecutionHandle> DisconnectCatalogOnKernelAsync(string nodeName, string kernelId, string catalogName, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsync(
            $"{Ws}/nodes/{nodeName}/kernels/{kernelId}/catalogs/{Uri.EscapeDataString(catalogName)}/disconnect", null, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ExecutionHandle>(JsonOptions, ct))!;
    }

    public async Task<List<string>> GetWorkspaceItemCatalogsAsync(Guid itemId, CancellationToken ct = default) =>
        await httpClient.GetFromJsonAsync<List<string>>($"{Ws}/items/{itemId}/catalogs", JsonOptions, ct) ?? [];

    public async Task UpdateWorkspaceItemCatalogsAsync(Guid itemId, List<string> catalogNames, CancellationToken ct = default)
    {
        var response = await httpClient.PutAsJsonAsync($"{Ws}/items/{itemId}/catalogs",
            new UpdateWorkspaceItemCatalogsRequest(catalogNames), JsonOptions, ct);
        response.EnsureSuccessStatusCode();
    }
}
