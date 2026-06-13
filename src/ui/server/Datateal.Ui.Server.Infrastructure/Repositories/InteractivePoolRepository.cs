using Datateal.Data;
using Datateal.Orchestrator.Core.Entities;
using Datateal.Ui.Server.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Datateal.Ui.Server.Infrastructure.Repositories;

internal class InteractivePoolRepository(DatatealDbContext dbContext) : IInteractivePoolRepository
{
    public async Task<InteractivePoolInfo?> GetByNameAsync(Guid workspaceId, string name, CancellationToken cancellationToken = default)
    {
        var pool = await dbContext.NodePoolConfigs
            .OfType<InteractiveNodePoolConfig>()
            .Where(p => p.WorkspaceId == workspaceId)
            .FirstOrDefaultAsync(p => p.Name == name, cancellationToken);

        return pool is null ? null : ToInfo(pool);
    }

    public async Task<IReadOnlyList<InteractivePoolInfo>> GetAllAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        var pools = await dbContext.NodePoolConfigs
            .OfType<InteractiveNodePoolConfig>()
            .Where(p => p.WorkspaceId == workspaceId)
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
