namespace DuckHouse.Ui.Shared.Workspace;

public record NotebookDetail(Guid Id, string Title, Guid? FolderId, DateTime CreatedAt, DateTime UpdatedAt, string Content, List<string>? CatalogNames = null);
