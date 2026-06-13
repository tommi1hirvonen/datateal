using System.Net;
using System.Net.Http.Json;
using Datateal.Ui.Shared.Workspaces;

namespace Datateal.Ui.Client.Services;

internal class WorkspaceManagementService(HttpClient httpClient) : IWorkspaceManagementService
{
    public async Task<IReadOnlyList<WorkspaceDto>> GetAccessibleAsync(CancellationToken ct = default) =>
        await httpClient.GetFromJsonAsync<IReadOnlyList<WorkspaceDto>>("api/workspaces", ct) ?? [];

    public async Task<WorkspaceDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var response = await httpClient.GetAsync($"api/workspaces/{id}", ct);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden)
            return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WorkspaceDto>(ct);
    }

    public async Task<WorkspaceDto> CreateAsync(CreateWorkspaceRequest request, CancellationToken ct = default)
    {
        var response = await httpClient.PostAsJsonAsync("api/workspaces", request, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<WorkspaceDto>(ct))!;
    }

    public async Task<WorkspaceDto?> UpdateAsync(Guid id, UpdateWorkspaceRequest request, CancellationToken ct = default)
    {
        var response = await httpClient.PutAsJsonAsync($"api/workspaces/{id}", request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WorkspaceDto>(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var response = await httpClient.DeleteAsync($"api/workspaces/{id}", ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<WorkspaceMemberDto>> GetMembersAsync(Guid workspaceId, CancellationToken ct = default) =>
        await httpClient.GetFromJsonAsync<IReadOnlyList<WorkspaceMemberDto>>($"api/workspaces/{workspaceId}/members", ct) ?? [];

    public async Task SetMemberAsync(Guid workspaceId, SetWorkspaceMemberRequest request, CancellationToken ct = default)
    {
        var response = await httpClient.PutAsJsonAsync($"api/workspaces/{workspaceId}/members", request, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task RemoveMemberAsync(Guid workspaceId, Guid userId, CancellationToken ct = default)
    {
        var response = await httpClient.DeleteAsync($"api/workspaces/{workspaceId}/members/{userId}", ct);
        response.EnsureSuccessStatusCode();
    }
}
