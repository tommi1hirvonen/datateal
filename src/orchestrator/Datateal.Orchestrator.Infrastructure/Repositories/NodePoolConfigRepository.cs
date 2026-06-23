using Datateal.Data;
using Datateal.Orchestrator.Core.Entities;
using Datateal.Orchestrator.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Datateal.Orchestrator.Infrastructure.Repositories;

internal class NodePoolConfigRepository(DatatealDbContext db) : INodePoolConfigRepository
{
    public async Task<IReadOnlyList<NodePoolConfig>> GetAllAsync(CancellationToken cancellationToken = default)
        => await db.NodePoolConfigs.OrderBy(c => c.Name).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<NodePoolConfig>> GetByWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default)
        => await db.NodePoolConfigs
            .Where(c => c.WorkspaceId == workspaceId)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

    public async Task<NodePoolConfig?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => await db.NodePoolConfigs.FindAsync([id], cancellationToken);

    public async Task<NodePoolConfig?> GetByNameAsync(string name, Guid workspaceId, CancellationToken cancellationToken = default)
        => await db.NodePoolConfigs.FirstOrDefaultAsync(c => c.WorkspaceId == workspaceId && c.Name == name, cancellationToken);

    public async Task<NodePoolConfig> CreateAsync(NodePoolConfig config, CancellationToken cancellationToken = default)
    {
        config.Id = Guid.CreateVersion7();
        config.CreatedAt = DateTime.UtcNow;
        config.UpdatedAt = DateTime.UtcNow;
        db.NodePoolConfigs.Add(config);
        await db.SaveChangesAsync(cancellationToken);
        return config;
    }

    public async Task<NodePoolConfig?> UpdateAsync(NodePoolConfig config, CancellationToken cancellationToken = default)
    {
        var existing = await db.NodePoolConfigs.FindAsync([config.Id], cancellationToken);
        if (existing is null) return null;

        existing.Name = config.Name;
        existing.VmSize = config.VmSize;
        existing.KernelIdleTimeout = config.KernelIdleTimeout;
        existing.NodeIdleTimeout = config.NodeIdleTimeout;
        existing.KernelRequirements = config.KernelRequirements;
        existing.Description = config.Description;
        existing.WheelPackageIds = config.WheelPackageIds;
        existing.EnvironmentVariableIds = config.EnvironmentVariableIds;
        existing.SecretIds = config.SecretIds;
        existing.UpdatedAt = DateTime.UtcNow;

        if (config is JobNodePoolConfig src && existing is JobNodePoolConfig dst)
        {
            dst.WarmNodes = src.WarmNodes;
            dst.MaxNodes = src.MaxNodes;
            dst.NodeAcquireTimeout = src.NodeAcquireTimeout;
        }

        await db.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var config = await db.NodePoolConfigs.FindAsync([id], cancellationToken);
        if (config is null) return false;
        db.NodePoolConfigs.Remove(config);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
