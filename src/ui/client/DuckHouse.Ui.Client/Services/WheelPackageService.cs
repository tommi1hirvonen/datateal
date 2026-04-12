using System.Net.Http.Json;
using System.Text.Json;
using DuckHouse.Ui.Shared.Packages;
using Microsoft.AspNetCore.Components.Forms;

namespace DuckHouse.Ui.Client.Services;

internal class WheelPackageService(HttpClient httpClient) : IWheelPackageService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<WheelPackageDto>> GetPackagesAsync(CancellationToken ct = default) =>
        await httpClient.GetFromJsonAsync<IReadOnlyList<WheelPackageDto>>("api/packages", JsonOptions, ct) ?? [];

    public async Task<WheelPackageDto> UploadPackageAsync(IBrowserFile file, CancellationToken ct = default)
    {
        using var content = new MultipartFormDataContent();
        var stream = file.OpenReadStream(maxAllowedSize: 5 * 1024 * 1024, cancellationToken: ct);
        content.Add(new StreamContent(stream), "file", file.Name);

        var response = await httpClient.PostAsync("api/packages", content, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<WheelPackageDto>(JsonOptions, ct))!;
    }

    public async Task DeletePackageAsync(Guid id, CancellationToken ct = default)
    {
        var response = await httpClient.DeleteAsync($"api/packages/{id}", ct);
        response.EnsureSuccessStatusCode();
    }
}
