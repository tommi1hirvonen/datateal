using Datateal.Data;
using Datateal.Orchestrator.Core.Entities;
using Datateal.Ui.Server.Core.Repositories;
using Datateal.Ui.Shared.Workspaces;
using Microsoft.EntityFrameworkCore;

namespace Datateal.Ui.Server.Infrastructure.Repositories;

internal class InteractivePoolRepository(DatatealDbContext dbContext, IActiveWorkspaceAccessor activeWorkspace) : IInteractivePoolRepository
{
    private Guid WorkspaceId => activeWorkspace.ActiveWorkspaceId
        ?? throw new InvalidOperationException("No active workspace is in scope for this request.");

    private IQueryable<InteractiveNodePoolConfig> Pools =>
        dbContext.NodePoolConfigs.OfType<InteractiveNodePoolConfig>().Where(p => p.WorkspaceId == WorkspaceId);

    public async Task<InteractivePoolInfo?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var pool = await Pools.FirstOrDefaultAsync(p => p.Name == name, cancellationToken);

        return pool is null ? null : ToInfo(pool);
    }

    public async Task<IReadOnlyList<InteractivePoolInfo>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var pools = await Pools
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

        return pools.Select(ToInfo).ToList();
    }

    private static InteractivePoolInfo ToInfo(InteractiveNodePoolConfig pool) => new(
        pool.Id,
        pool.Name,
        pool.GetNodeName(),
        pool.VmSize,
        pool.KernelIdleTimeout,
        pool.NodeIdleTimeout,
        pool.KernelRequirements,
        pool.Description,
        pool.WheelPackageIds,
        pool.EnvironmentVariableIds,
        pool.SecretIds);
}
