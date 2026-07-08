using System.Net.Http.Json;
using Datateal.Ui.Shared.Users;

namespace Datateal.Ui.Client.Services;

internal class ServiceAccountService(HttpClient httpClient) : IServiceAccountService
{
    public async Task<IReadOnlyList<ServiceAccountDto>> GetServiceAccountsAsync(CancellationToken ct = default) =>
        await httpClient.GetFromJsonAsync<IReadOnlyList<ServiceAccountDto>>("api/users/service-accounts", ct) ?? [];

    public async Task<ServiceAccountDto> CreateServiceAccountAsync(CreateServiceAccountRequest request, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsJsonAsync("api/users/service-accounts", request, ct);
        await response.EnsureSuccessWithDetailsAsync(ct);
        return (await response.Content.ReadFromJsonAsync<ServiceAccountDto>(ct))!;
    }

    public async Task<ServiceAccountDto?> UpdateServiceAccountAsync(Guid id, UpdateServiceAccountRequest request, CancellationToken ct = default)
    {
        var response = await httpClient.PutAsJsonAsync($"api/users/service-accounts/{id}", request, ct);
        await response.EnsureSuccessWithDetailsAsync(ct);
        return await response.Content.ReadFromJsonAsync<ServiceAccountDto>(ct);
    }

    public async Task DeleteServiceAccountAsync(Guid id, CancellationToken ct = default)
    {
        var response = await httpClient.DeleteAsync($"api/users/service-accounts/{id}", ct);
        await response.EnsureSuccessWithDetailsAsync(ct);
    }
}
