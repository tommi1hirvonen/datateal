using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DuckHouse.Ui.Shared.Workspace;

namespace DuckHouse.Ui.Client.Services;

internal class WorkspaceService(HttpClient httpClient) : IWorkspaceService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<WorkspaceListing> GetRootAsync(CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<WorkspaceListing>("api/workspace", JsonOptions, cancellationToken)
        ?? new WorkspaceListing([], [], []);

    public async Task<WorkspaceListing> GetFolderAsync(Guid folderId, CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<WorkspaceListing>($"api/workspace/folders/{folderId}", JsonOptions, cancellationToken)
        ?? new WorkspaceListing([], [], []);

    public async Task<IReadOnlyList<FolderSummary>> GetFolderAncestorsAsync(Guid folderId, CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<IReadOnlyList<FolderSummary>>($"api/workspace/folders/{folderId}/ancestors", JsonOptions, cancellationToken)
        ?? [];

    public async Task<NotebookDetail?> GetNotebookAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"api/workspace/notebooks/{id}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<NotebookDetail>(JsonOptions, cancellationToken);
    }

    public async Task<FolderSummary> CreateFolderAsync(CreateFolderRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("api/workspace/folders", request, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<FolderSummary>(JsonOptions, cancellationToken))!;
    }

    public async Task<FolderSummary?> UpdateFolderAsync(Guid id, UpdateFolderRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync($"api/workspace/folders/{id}", request, JsonOptions, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<FolderSummary>(JsonOptions, cancellationToken);
    }

    public async Task DeleteFolderAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync($"api/workspace/folders/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<NotebookSummary> CreateNotebookAsync(CreateNotebookRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("api/workspace/notebooks", request, JsonOptions, cancellationToken);
        await EnsureNotConflictAsync(response, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<NotebookSummary>(JsonOptions, cancellationToken))!;
    }

    public async Task<NotebookSummary?> UpdateNotebookAsync(Guid id, UpdateNotebookRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync($"api/workspace/notebooks/{id}", request, JsonOptions, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await EnsureNotConflictAsync(response, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<NotebookSummary>(JsonOptions, cancellationToken);
    }

    public async Task DeleteNotebookAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync($"api/workspace/notebooks/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<QueryDetail?> GetQueryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"api/workspace/queries/{id}", cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<QueryDetail>(JsonOptions, cancellationToken);
    }

    public async Task<QuerySummary> CreateQueryAsync(CreateQueryRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("api/workspace/queries", request, JsonOptions, cancellationToken);
        await EnsureNotConflictAsync(response, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<QuerySummary>(JsonOptions, cancellationToken))!;
    }

    public async Task<QuerySummary?> UpdateQueryAsync(Guid id, UpdateQueryRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync($"api/workspace/queries/{id}", request, JsonOptions, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await EnsureNotConflictAsync(response, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<QuerySummary>(JsonOptions, cancellationToken);
    }

    public async Task DeleteQueryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync($"api/workspace/queries/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task SaveQueryResultAsync(Guid id, SaveQueryResultRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync($"api/workspace/queries/{id}/result", request, JsonOptions, cancellationToken);
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
