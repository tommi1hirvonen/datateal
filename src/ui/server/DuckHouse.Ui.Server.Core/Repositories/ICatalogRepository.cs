using DuckHouse.Core.Catalogs;

namespace DuckHouse.Ui.Server.Core.Repositories;

public interface ICatalogRepository
{
    Task<IReadOnlyList<Catalog>> GetAllAsync(CancellationToken ct = default);
    Task<Catalog?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Catalog?> GetByNameAsync(string name, CancellationToken ct = default);
    Task<bool> CatalogNameExistsAsync(string name, Guid? excludeId = null, CancellationToken ct = default);
    Task<Catalog> CreateAsync(Catalog catalog, CancellationToken ct = default);
    Task<Catalog?> UpdateAsync(Catalog catalog, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Catalog>> GetByNamesAsync(IReadOnlyList<string> names, CancellationToken ct = default);
}
