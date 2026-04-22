namespace DuckHouse.Ui.Client.Services;

public record RecentItem(Guid Id, string Name, string Type, DateTime AccessedAt);

public interface IRecentItemsService
{
    Task<IReadOnlyList<RecentItem>> GetRecentItemsAsync();
    Task RecordVisitAsync(Guid id, string name, string type);
    Task RemoveAsync(Guid id);
    Task UpdateNameAsync(Guid id, string newName);
}
