using Datateal.Core.RuntimePackages;
using Datateal.Data;
using Datateal.Ui.Server.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Datateal.Ui.Server.Infrastructure.Repositories;

internal class WheelPackageRepository(DatatealDbContext dbContext) : IWheelPackageRepository
{
    public async Task<IReadOnlyList<WheelPackage>> GetAllAsync(Guid workspaceId, CancellationToken cancellationToken = default) =>
        await dbContext.WheelPackages
            .Where(p => p.WorkspaceId == workspaceId)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

    public async Task<WheelPackage?> GetByIdAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken = default) =>
        await dbContext.WheelPackages
            .Where(p => p.WorkspaceId == workspaceId)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyList<WheelPackage>> GetByIdsAsync(Guid workspaceId, IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.ToList();
        return await dbContext.WheelPackages
            .Where(p => p.WorkspaceId == workspaceId)
            .Where(p => idList.Contains(p.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsByNameAsync(Guid workspaceId, string name, CancellationToken cancellationToken = default) =>
        await dbContext.WheelPackages
            .Where(p => p.WorkspaceId == workspaceId)
            .AnyAsync(p => p.Name == name, cancellationToken);

    public async Task<WheelPackage> AddAsync(Guid workspaceId, WheelPackage package, CancellationToken cancellationToken = default)
    {
        package.WorkspaceId = workspaceId;
        dbContext.WheelPackages.Add(package);
        await dbContext.SaveChangesAsync(cancellationToken);
        return package;
    }

    public async Task<bool> DeleteAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken = default)
    {
        var package = await dbContext.WheelPackages
            .Where(p => p.WorkspaceId == workspaceId)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (package is null) return false;
        dbContext.WheelPackages.Remove(package);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
