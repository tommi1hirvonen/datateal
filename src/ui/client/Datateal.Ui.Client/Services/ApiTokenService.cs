using System.Net.Http.Json;
using Datateal.Ui.Shared.ApiTokens;

namespace Datateal.Ui.Client.Services;

internal class ApiTokenService(HttpClient httpClient) : IApiTokenService
{
    public async Task<IReadOnlyList<ApiTokenDto>> GetTokensAsync(CancellationToken ct = default) =>
        await httpClient.GetFromJsonAsync<IReadOnlyList<ApiTokenDto>>("api/tokens", ct) ?? [];

    public async Task<CreateApiTokenResponse> CreateTokenAsync(CreateApiTokenRequest request, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsJsonAsync("api/tokens", request, ct);
        await response.EnsureSuccessWithDetailsAsync(ct);
        return (await response.Content.ReadFromJsonAsync<CreateApiTokenResponse>(ct))!;
    }

    public async Task RevokeTokenAsync(Guid id, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsync($"api/tokens/{id}/revoke", null, ct);
        await response.EnsureSuccessWithDetailsAsync(ct);
    }

    public async Task DeleteTokenAsync(Guid id, CancellationToken ct = default)
    {
        var response = await httpClient.DeleteAsync($"api/tokens/{id}", ct);
        await response.EnsureSuccessWithDetailsAsync(ct);
    }
}
