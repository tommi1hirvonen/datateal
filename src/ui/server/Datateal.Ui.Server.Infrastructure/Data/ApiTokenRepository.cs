using Datateal.Core.ApiTokens;
using Datateal.Data;
using Datateal.Ui.Server.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Datateal.Ui.Server.Infrastructure.Data;

internal class ApiTokenRepository(DatatealDbContext db) : IApiTokenRepository
{
    public async Task<IReadOnlyList<ApiToken>> GetAllAsync(Guid? workspaceId = null, CancellationToken ct = default)
    {
        var query = db.ApiTokens.AsNoTracking().AsQueryable();
        if (workspaceId is not null)
            query = query.Where(t => t.WorkspaceId == workspaceId);
        return await query.OrderByDescending(t => t.CreatedAt).ToListAsync(ct);
    }

    public Task<ApiToken?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.ApiTokens.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<IReadOnlyList<ApiToken>> GetByPrefixAsync(string prefix, CancellationToken ct = default) =>
        await db.ApiTokens.AsNoTracking()
            .Where(t => t.TokenPrefix == prefix)
            .ToListAsync(ct);

    public async Task<ApiToken> CreateAsync(ApiToken token, CancellationToken ct = default)
    {
        db.ApiTokens.Add(token);
        await db.SaveChangesAsync(ct);
        return token;
    }

    public async Task<bool> RevokeAsync(Guid id, CancellationToken ct = default)
    {
        var affected = await db.ApiTokens
            .Where(t => t.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.IsRevoked, true)
                .SetProperty(t => t.UpdatedAt, DateTime.UtcNow), ct);
        return affected > 0;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var affected = await db.ApiTokens.Where(t => t.Id == id).ExecuteDeleteAsync(ct);
        return affected > 0;
    }

    public Task TouchLastUsedAsync(Guid id, DateTime usedAt, CancellationToken ct = default) =>
        db.ApiTokens
            .Where(t => t.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.LastUsedAt, usedAt), ct);
}
