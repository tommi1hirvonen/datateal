using Datateal.Core.RuntimePackages;
using Datateal.Data;
using Datateal.Ui.Server.Core.Repositories;
using Datateal.Ui.Shared.Workspaces;
using Microsoft.EntityFrameworkCore;

namespace Datateal.Ui.Server.Infrastructure.Repositories;

internal class WheelPackageRepository(DatatealDbContext dbContext, IActiveWorkspaceAccessor activeWorkspace) : IWheelPackageRepository
{
    private Guid WorkspaceId => activeWorkspace.ActiveWorkspaceId
        ?? throw new InvalidOperationException("No active workspace is in scope for this request.");

    private IQueryable<WheelPackage> Packages => dbContext.WheelPackages.Where(p => p.WorkspaceId == WorkspaceId);

    public async Task<IReadOnlyList<WheelPackage>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await Packages
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

    public async Task<WheelPackage?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await Packages.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public async Task<IReadOnlyList<WheelPackage>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var idList = ids.ToList();
        return await Packages
            .Where(p => idList.Contains(p.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken = default) =>
        await Packages.AnyAsync(p => p.Name == name, cancellationToken);

    public async Task<WheelPackage> AddAsync(WheelPackage package, CancellationToken cancellationToken = default)
    {
        package.WorkspaceId = WorkspaceId;
        dbContext.WheelPackages.Add(package);
        await dbContext.SaveChangesAsync(cancellationToken);
        return package;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var package = await Packages.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (package is null) return false;
        dbContext.WheelPackages.Remove(package);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
