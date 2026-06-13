namespace Datateal.Ui.Client.Services;

public record RecentItem(Guid Id, string Name, string Type, Guid WorkspaceId, DateTime AccessedAt);

public interface IRecentItemsService
{
    /// <summary>Returns the most recently visited items within the given workspace.</summary>
    Task<IReadOnlyList<RecentItem>> GetRecentItemsAsync(Guid workspaceId);
    Task RecordVisitAsync(Guid id, string name, string type, Guid workspaceId);
    Task RemoveAsync(Guid id);
    Task UpdateNameAsync(Guid id, string newName);
}
