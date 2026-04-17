using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
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

    public async Task<CatalogDto> CreateCatalogAsync(CreateCatalogRequest request, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsJsonAsync("api/catalogs", request, JsonOptions, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CatalogDto>(JsonOptions, ct))!;
    }

    public async Task<CatalogDto?> UpdateCatalogAsync(Guid id, UpdateCatalogRequest request, CancellationToken ct = default)
    {
        var response = await httpClient.PutAsJsonAsync($"api/catalogs/{id}", request, JsonOptions, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CatalogDto>(JsonOptions, ct);
    }

    public async Task DeleteCatalogAsync(Guid id, CancellationToken ct = default)
    {
        var response = await httpClient.DeleteAsync($"api/catalogs/{id}", ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<CatalogMetadataDto> GetMetadataAsync(Guid catalogId, CancellationToken ct = default) =>
        await httpClient.GetFromJsonAsync<CatalogMetadataDto>($"api/catalogs/{catalogId}/metadata", JsonOptions, ct)
        ?? new CatalogMetadataDto([]);

    public async Task<IReadOnlyList<ResolvedCatalogDto>> ResolveCatalogsAsync(List<string> catalogNames, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsJsonAsync("api/catalogs/resolve",
            new ResolveCatalogsRequest(catalogNames), JsonOptions, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IReadOnlyList<ResolvedCatalogDto>>(JsonOptions, ct)) ?? [];
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
