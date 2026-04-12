using DuckHouse.Core.Nodes;
using DuckHouse.Orchestrator.Core.Interfaces;
using DuckHouse.Orchestrator.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DuckHouse.Orchestrator.Infrastructure.Repositories;

internal class WheelPackageReader(OrchestratorDbContext dbContext) : IWheelPackageReader
{
    public async Task<IReadOnlyList<WheelContent>> GetWheelContentsAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        var idList = ids.ToList();
        if (idList.Count == 0) return [];

        var conn = dbContext.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync(ct);

        var results = new List<WheelContent>();

        foreach (var id in idList)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """SELECT "FileName", "Data" FROM "WheelPackages" WHERE "Id" = @id""";

            var p = cmd.CreateParameter();
            p.ParameterName = "@id";
            p.Value = id;
            cmd.Parameters.Add(p);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                var fileName = reader.GetString(0);
                var data = (byte[])reader.GetValue(1);
                results.Add(new WheelContent(fileName, data));
            }
        }

        return results;
    }
}
