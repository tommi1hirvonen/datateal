using DuckHouse.Ui.Shared.Catalogs;

namespace DuckHouse.Ui.Client.Components;

public class CatalogObjectSelection
{
    public string CatalogName { get; init; } = "";
    public string SchemaName { get; init; } = "";
    public string ObjectName { get; init; } = "";
    public string ObjectType { get; init; } = "";
    public TableDto Table { get; init; } = null!;
}
