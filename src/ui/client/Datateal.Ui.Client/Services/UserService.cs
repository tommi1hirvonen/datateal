using System.Net.Http.Json;
using Datateal.Ui.Shared.Users;

namespace Datateal.Ui.Client.Services;

internal class UserService(HttpClient httpClient) : IUserService
{
    public async Task<IReadOnlyList<AppUserDto>> GetUsersAsync(CancellationToken ct = default) =>
        await httpClient.GetFromJsonAsync<IReadOnlyList<AppUserDto>>("api/users", ct) ?? [];

    public async Task<AppUserDto?> GetUserAsync(Guid id, CancellationToken ct = default) =>
        await httpClient.GetFromJsonAsync<AppUserDto>($"api/users/{id}", ct);

    public async Task<AppUserDto> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsJsonAsync("api/users", request, ct);
        await response.EnsureSuccessWithDetailsAsync(ct);
        return (await response.Content.ReadFromJsonAsync<AppUserDto>(ct))!;
    }

    public async Task<AppUserDto?> UpdateUserAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default)
    {
        var response = await httpClient.PutAsJsonAsync($"api/users/{id}", request, ct);
        await response.EnsureSuccessWithDetailsAsync(ct);
        return await response.Content.ReadFromJsonAsync<AppUserDto>(ct);
    }

    public async Task DeleteUserAsync(Guid id, CancellationToken ct = default)
    {
        var response = await httpClient.DeleteAsync($"api/users/{id}", ct);
        await response.EnsureSuccessWithDetailsAsync(ct);
    }
}
