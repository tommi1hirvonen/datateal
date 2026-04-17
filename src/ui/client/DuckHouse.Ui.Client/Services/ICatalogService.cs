using DuckHouse.Ui.Shared.Catalogs;

namespace DuckHouse.Ui.Client.Services;

public interface ICatalogService
{
    Task<IReadOnlyList<CatalogDto>> GetCatalogsAsync(CancellationToken ct = default);
    Task<CatalogDto> CreateCatalogAsync(CreateCatalogRequest request, CancellationToken ct = default);
    Task<CatalogDto?> UpdateCatalogAsync(Guid id, UpdateCatalogRequest request, CancellationToken ct = default);
    Task DeleteCatalogAsync(Guid id, CancellationToken ct = default);
    Task<CatalogMetadataDto> GetMetadataAsync(Guid catalogId, CancellationToken ct = default);
    Task<IReadOnlyList<ResolvedCatalogDto>> ResolveCatalogsAsync(List<string> catalogNames, CancellationToken ct = default);

    // Workspace item catalog associations
    Task<List<string>> GetWorkspaceItemCatalogsAsync(Guid itemId, CancellationToken ct = default);
    Task UpdateWorkspaceItemCatalogsAsync(Guid itemId, List<string> catalogNames, CancellationToken ct = default);
}
