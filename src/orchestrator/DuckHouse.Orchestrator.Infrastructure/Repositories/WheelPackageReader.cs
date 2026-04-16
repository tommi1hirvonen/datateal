using DuckHouse.Core.Nodes;
using DuckHouse.Data;
using DuckHouse.Orchestrator.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DuckHouse.Orchestrator.Infrastructure.Repositories;

internal class WheelPackageReader(DuckHouseDbContext db) : IWheelPackageReader
{
    public async Task<IReadOnlyList<WheelContent>> GetWheelContentsAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var idList = ids.ToList();
        if (idList.Count == 0) return [];

        return await db.WheelPackages
            .Where(p => idList.Contains(p.Id))
            .Select(p => new WheelContent(p.FileName, p.Data))
            .ToListAsync(ct);
    }
}

