using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Datateal.Ui.Shared.Workspace;
using Datateal.Ui.Shared.Workspaces;

namespace Datateal.Ui.Client.Services;

internal class WorkspaceService(HttpClient httpClient, IActiveWorkspaceAccessor workspace) : IWorkspaceService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private string Ws => $"api/workspaces/{workspace.ActiveWorkspaceId!.Value}/items";

    public async Task<WorkspaceSearchResult> SearchAsync(string query, CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<WorkspaceSearchResult>(
            $"{Ws}/search?q={Uri.EscapeDataString(query)}", JsonOptions, cancellationToken)
        ?? new WorkspaceSearchResult([]);

    public async Task<WorkspaceListing> GetRootAsync(CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<WorkspaceListing>(Ws, JsonOptions, cancellationToken)
        ?? new WorkspaceListing([], []);

    public async Task<WorkspaceListing> GetFolderAsync(Guid folderId, CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<WorkspaceListing>($"{Ws}/folders/{folderId}", JsonOptions, cancellationToken)
        ?? new WorkspaceListing([], []);

    public async Task<IReadOnlyList<FolderSummary>> GetFolderAncestorsAsync(Guid folderId, CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<IReadOnlyList<FolderSummary>>($"{Ws}/folders/{folderId}/ancestors", JsonOptions, cancellationToken)
        ?? [];

    public async Task<NotebookDetail?> GetNotebookAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"{Ws}/notebooks/{id}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<NotebookDetail>(JsonOptions, cancellationToken);
    }

    public async Task<ResolvedWorkspaceItem?> ResolvePathAsync(string relativePath, Guid? baseFolderId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync(
            $"{Ws}/resolve",
            new ResolvePathRequest(relativePath, baseFolderId),
            JsonOptions,
            cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ResolvedWorkspaceItem>(JsonOptions, cancellationToken);
    }

    public async Task<FolderSummary> CreateFolderAsync(CreateFolderRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync($"{Ws}/folders", request, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<FolderSummary>(JsonOptions, cancellationToken))!;
    }

    public async Task<FolderSummary?> UpdateFolderAsync(Guid id, UpdateFolderRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync($"{Ws}/folders/{id}", request, JsonOptions, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<FolderSummary>(JsonOptions, cancellationToken);
    }

    public async Task DeleteFolderAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync($"{Ws}/folders/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<WorkspaceItemSummary> CreateNotebookAsync(CreateNotebookRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync($"{Ws}/notebooks", request, JsonOptions, cancellationToken);
        await EnsureNotConflictAsync(response, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<WorkspaceItemSummary>(JsonOptions, cancellationToken))!;
    }

    public async Task<WorkspaceItemSummary?> UpdateNotebookAsync(Guid id, UpdateNotebookRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync($"{Ws}/notebooks/{id}", request, JsonOptions, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await EnsureNotConflictAsync(response, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WorkspaceItemSummary>(JsonOptions, cancellationToken);
    }

    public async Task DeleteNotebookAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync($"{Ws}/notebooks/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<QueryDetail?> GetQueryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"{Ws}/queries/{id}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<QueryDetail>(JsonOptions, cancellationToken);
    }

    public async Task<WorkspaceItemSummary> CreateQueryAsync(CreateQueryRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync($"{Ws}/queries", request, JsonOptions, cancellationToken);
        await EnsureNotConflictAsync(response, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<WorkspaceItemSummary>(JsonOptions, cancellationToken))!;
    }

    public async Task<WorkspaceItemSummary?> UpdateQueryAsync(Guid id, UpdateQueryRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync($"{Ws}/queries/{id}", request, JsonOptions, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await EnsureNotConflictAsync(response, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WorkspaceItemSummary>(JsonOptions, cancellationToken);
    }

    public async Task DeleteQueryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync($"{Ws}/queries/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static async Task EnsureNotConflictAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode != HttpStatusCode.Conflict) return;

        string? detail = null;
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ConflictProblem>(JsonOptions, cancellationToken);
            detail = problem?.Detail;
        }
        catch { /* fall through to default message */ }

        throw new InvalidOperationException(detail ?? "A notebook or query with this title already exists in the selected folder.");
    }

    private sealed record ConflictProblem(string? Detail);
}
