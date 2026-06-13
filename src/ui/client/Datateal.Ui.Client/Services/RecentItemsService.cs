using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.JSInterop;

namespace Datateal.Ui.Client.Services;

public sealed class RecentItemsService(IJSRuntime js) : IRecentItemsService
{
    private const string StorageKey = "datateal-recent-items";
    private const int MaxItems = 10;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<IReadOnlyList<RecentItem>> GetRecentItemsAsync(Guid workspaceId)
    {
        var all = await GetAllAsync();
        return all.Where(i => i.WorkspaceId == workspaceId).ToList();
    }

    private async Task<List<RecentItem>> GetAllAsync()
    {
        try
        {
            var json = await js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            if (string.IsNullOrEmpty(json)) return [];
            return JsonSerializer.Deserialize<List<RecentItem>>(json, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public async Task RecordVisitAsync(Guid id, string name, string type, Guid workspaceId)
    {
        try
        {
            var items = await GetAllAsync();
            items.RemoveAll(i => i.Id == id);
            items.Insert(0, new RecentItem(id, name, type, workspaceId, DateTime.UtcNow));

            // Cap the number of items retained per workspace (preserving recency order).
            var perWorkspace = new Dictionary<Guid, int>();
            var capped = new List<RecentItem>();
            foreach (var item in items)
            {
                perWorkspace.TryGetValue(item.WorkspaceId, out var count);
                if (count >= MaxItems) continue;
                capped.Add(item);
                perWorkspace[item.WorkspaceId] = count + 1;
            }

            var json = JsonSerializer.Serialize(capped, JsonOptions);
            await js.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
        }
        catch
        {
            // localStorage unavailable — silently ignore
        }
    }

    public async Task RemoveAsync(Guid id)
    {
        try
        {
            var items = await GetAllAsync();
            items.RemoveAll(i => i.Id == id);
            var json = JsonSerializer.Serialize(items, JsonOptions);
            await js.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
        }
        catch { }
    }

    public async Task UpdateNameAsync(Guid id, string newName)
    {
        try
        {
            var items = await GetAllAsync();
            var idx = items.FindIndex(i => i.Id == id);
            if (idx < 0) return;
            items[idx] = items[idx] with { Name = newName };
            var json = JsonSerializer.Serialize(items, JsonOptions);
            await js.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
        }
        catch { }
    }
}
