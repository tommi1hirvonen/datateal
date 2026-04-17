namespace DuckHouse.Ui.Shared.Catalogs;

public record CatalogMetadataDto(
    IReadOnlyList<SchemaDto> Schemas);

public record SchemaDto(
    string Name,
    IReadOnlyList<TableDto> Tables);

public record TableDto(
    string Name,
    string Type,
    IReadOnlyList<ColumnDto> Columns);

public record ColumnDto(
    string Name,
    string DataType,
    bool IsNullable,
    int OrdinalPosition);
