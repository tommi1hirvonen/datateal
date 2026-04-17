namespace DuckHouse.Ui.Shared.Workspace;

public record QueryDetail(Guid Id, string Title, Guid? FolderId, DateTime CreatedAt, DateTime UpdatedAt, string Content, QueryLastResult? LastResult = null, List<string>? CatalogNames = null);
