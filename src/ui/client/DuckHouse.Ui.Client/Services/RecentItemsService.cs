using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.JSInterop;

namespace DuckHouse.Ui.Client.Services;

public sealed class RecentItemsService(IJSRuntime js) : IRecentItemsService
{
    private const string StorageKey = "duckhouse-recent-items";
    private const int MaxItems = 10;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<IReadOnlyList<RecentItem>> GetRecentItemsAsync()
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

    public async Task RecordVisitAsync(Guid id, string name, string type)
    {
        try
        {
            var items = (await GetRecentItemsAsync()).ToList();
            items.RemoveAll(i => i.Id == id);
            items.Insert(0, new RecentItem(id, name, type, DateTime.UtcNow));
            if (items.Count > MaxItems)
                items = items[..MaxItems];

            var json = JsonSerializer.Serialize(items, JsonOptions);
            await js.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
        }
        catch
        {
            // localStorage unavailable — silently ignore
        }
    }
}
