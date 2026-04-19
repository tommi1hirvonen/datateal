using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DuckHouse.Core.Kernels;
using DuckHouse.Ui.Shared.Catalogs;

namespace DuckHouse.Ui.Client.Services;

internal class CatalogService(HttpClient httpClient) : ICatalogService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<IReadOnlyList<CatalogDto>> GetCatalogsAsync(CancellationToken ct = default) =>
        await httpClient.GetFromJsonAsync<IReadOnlyList<CatalogDto>>("api/catalogs", JsonOptions, ct) ?? [];

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
        await httpClient.GetFromJsonAsync<CatalogMetadataDto>($"api/catalogs/{catalogId}/metadata", JsonOptions, ct)
        ?? new CatalogMetadataDto([]);

    public async Task<ExecutionHandle> SetupCatalogsOnKernelAsync(string nodeName, string kernelId, List<string> catalogNames, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsJsonAsync(
            $"api/nodes/{nodeName}/kernels/{kernelId}/catalogs/setup",
            new KernelCatalogSetupRequest(catalogNames), JsonOptions, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ExecutionHandle>(JsonOptions, ct))!;
    }

    public async Task<ExecutionHandle> ConnectCatalogOnKernelAsync(string nodeName, string kernelId, string catalogName, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsync(
            $"api/nodes/{nodeName}/kernels/{kernelId}/catalogs/{Uri.EscapeDataString(catalogName)}/connect", null, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ExecutionHandle>(JsonOptions, ct))!;
    }

    public async Task<ExecutionHandle> DisconnectCatalogOnKernelAsync(string nodeName, string kernelId, string catalogName, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsync(
            $"api/nodes/{nodeName}/kernels/{kernelId}/catalogs/{Uri.EscapeDataString(catalogName)}/disconnect", null, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ExecutionHandle>(JsonOptions, ct))!;
    }

    public async Task<List<string>> GetWorkspaceItemCatalogsAsync(Guid itemId, CancellationToken ct = default) =>
        await httpClient.GetFromJsonAsync<List<string>>($"api/workspace/items/{itemId}/catalogs", JsonOptions, ct) ?? [];

    public async Task UpdateWorkspaceItemCatalogsAsync(Guid itemId, List<string> catalogNames, CancellationToken ct = default)
    {
        var response = await httpClient.PutAsJsonAsync($"api/workspace/items/{itemId}/catalogs",
            new UpdateWorkspaceItemCatalogsRequest(catalogNames), JsonOptions, ct);
        response.EnsureSuccessStatusCode();
    }
}
