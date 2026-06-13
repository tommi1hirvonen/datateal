using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Datateal.Ui.Shared.Environment;
using Datateal.Ui.Shared.Workspaces;

namespace Datateal.Ui.Client.Services;

internal class EnvironmentService(HttpClient httpClient, IActiveWorkspaceAccessor workspace) : IEnvironmentService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private string Ws => $"api/workspaces/{workspace.ActiveWorkspaceId!.Value}/environment";

    // ── Variables ────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<EnvironmentVariableDto>> GetVariablesAsync(CancellationToken ct) =>
        await httpClient.GetFromJsonAsync<IReadOnlyList<EnvironmentVariableDto>>(
            $"{Ws}/variables", JsonOptions, ct) ?? [];

    public async Task<EnvironmentVariableDto> CreateVariableAsync(CreateEnvironmentVariableRequest request, CancellationToken ct)
    {
        var response = await httpClient.PostAsJsonAsync($"{Ws}/variables", request, JsonOptions, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EnvironmentVariableDto>(JsonOptions, ct))!;
    }

    public async Task<EnvironmentVariableDto?> UpdateVariableAsync(Guid id, UpdateEnvironmentVariableRequest request, CancellationToken ct)
    {
        var response = await httpClient.PutAsJsonAsync($"{Ws}/variables/{id}", request, JsonOptions, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<EnvironmentVariableDto>(JsonOptions, ct);
    }

    public async Task DeleteVariableAsync(Guid id, CancellationToken ct)
    {
        var response = await httpClient.DeleteAsync($"{Ws}/variables/{id}", ct);
        response.EnsureSuccessStatusCode();
    }

    // ── Secrets ──────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<SecretDto>> GetSecretsAsync(CancellationToken ct) =>
        await httpClient.GetFromJsonAsync<IReadOnlyList<SecretDto>>(
            $"{Ws}/secrets", JsonOptions, ct) ?? [];

    public async Task<SecretDto> CreateSecretAsync(CreateSecretRequest request, CancellationToken ct)
    {
        var response = await httpClient.PostAsJsonAsync($"{Ws}/secrets", request, JsonOptions, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SecretDto>(JsonOptions, ct))!;
    }

    public async Task<SecretDto?> UpdateSecretAsync(Guid id, UpdateSecretRequest request, CancellationToken ct)
    {
        var response = await httpClient.PutAsJsonAsync($"{Ws}/secrets/{id}", request, JsonOptions, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SecretDto>(JsonOptions, ct);
    }

    public async Task DeleteSecretAsync(Guid id, CancellationToken ct)
    {
        var response = await httpClient.DeleteAsync($"{Ws}/secrets/{id}", ct);
        response.EnsureSuccessStatusCode();
    }
}
