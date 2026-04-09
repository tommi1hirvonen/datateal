using DuckHouse.ControlPlane.Core.Nodes;
using DuckHouse.ControlPlane.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DuckHouse.ControlPlane.Infrastructure.Data;

internal class NodeConfigRepository(ControlPlaneDbContext db) : INodeConfigRepository
{
    public Task<NodeConfig?> GetAsync(string nodeName, CancellationToken cancellationToken = default) =>
        db.NodeConfigs.FirstOrDefaultAsync(c => c.NodeName == nodeName, cancellationToken);

    public async Task UpsertAsync(NodeConfig config, CancellationToken cancellationToken = default)
    {
        var existing = await db.NodeConfigs
            .FirstOrDefaultAsync(c => c.NodeName == config.NodeName, cancellationToken);

        if (existing is null)
        {
            db.NodeConfigs.Add(config);
        }
        else
        {
            existing.KernelIdleTimeout = config.KernelIdleTimeout;
            existing.NodeIdleTimeout = config.NodeIdleTimeout;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string nodeName, CancellationToken cancellationToken = default)
    {
        var existing = await db.NodeConfigs
            .FirstOrDefaultAsync(c => c.NodeName == nodeName, cancellationToken);

        if (existing is not null)
        {
            db.NodeConfigs.Remove(existing);
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
