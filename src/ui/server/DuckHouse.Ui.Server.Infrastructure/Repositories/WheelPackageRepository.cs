using DuckHouse.Core.RuntimePackages;
using DuckHouse.Data;
using DuckHouse.Ui.Server.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DuckHouse.Ui.Server.Infrastructure.Repositories;

internal class WheelPackageRepository(DuckHouseDbContext dbContext) : IWheelPackageRepository
{
    public async Task<IReadOnlyList<WheelPackage>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await dbContext.WheelPackages
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

    public async Task<WheelPackage?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await dbContext.WheelPackages.FindAsync([id], cancellationToken);

    public async Task<IReadOnlyList<WheelPackage>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.ToList();
        return await dbContext.WheelPackages
            .Where(p => idList.Contains(p.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken = default) =>
        await dbContext.WheelPackages.AnyAsync(p => p.Name == name, cancellationToken);

    public async Task<WheelPackage> AddAsync(WheelPackage package, CancellationToken cancellationToken = default)
    {
        dbContext.WheelPackages.Add(package);
        await dbContext.SaveChangesAsync(cancellationToken);
        return package;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var package = await dbContext.WheelPackages.FindAsync([id], cancellationToken);
        if (package is null) return false;
        dbContext.WheelPackages.Remove(package);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
