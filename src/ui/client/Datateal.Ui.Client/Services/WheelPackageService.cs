using System.Net.Http.Json;
using System.Text.Json;
using Datateal.Ui.Shared.RuntimePackages;
using Datateal.Ui.Shared.Workspaces;
using Microsoft.AspNetCore.Components.Forms;

namespace Datateal.Ui.Client.Services;

internal class WheelPackageService(HttpClient httpClient, IActiveWorkspaceAccessor workspace) : IWheelPackageService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private string Ws => $"api/workspaces/{workspace.ActiveWorkspaceId!.Value}/packages";

    public async Task<IReadOnlyList<WheelPackageDto>> GetPackagesAsync(CancellationToken ct = default) =>
        await httpClient.GetFromJsonAsync<IReadOnlyList<WheelPackageDto>>(Ws, JsonOptions, ct) ?? [];

    public async Task<WheelPackageDto> UploadPackageAsync(IBrowserFile file, CancellationToken ct = default)
    {
        using var content = new MultipartFormDataContent();
        var stream = file.OpenReadStream(maxAllowedSize: 5 * 1024 * 1024, cancellationToken: ct);
        content.Add(new StreamContent(stream), "file", file.Name);

        var response = await httpClient.PostAsync(Ws, content, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<WheelPackageDto>(JsonOptions, ct))!;
    }

    public async Task DeletePackageAsync(Guid id, CancellationToken ct = default)
    {
        var response = await httpClient.DeleteAsync($"{Ws}/{id}", ct);
        response.EnsureSuccessStatusCode();
    }
}
