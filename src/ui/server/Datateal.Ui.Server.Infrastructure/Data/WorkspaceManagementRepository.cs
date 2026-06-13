using Datateal.Core.Workspaces;
using Datateal.Data;
using Datateal.Ui.Server.Core.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Datateal.Ui.Server.Infrastructure.Data;

internal class WorkspaceManagementRepository(DatatealDbContext db) : IWorkspaceManagementRepository
{
    public async Task<IReadOnlyList<Workspace>> GetAllAsync(CancellationToken ct = default) =>
        await db.Workspaces.OrderBy(w => w.Name).ToListAsync(ct);

    public async Task<IReadOnlyList<Workspace>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default) =>
        await db.Workspaces.Where(w => ids.Contains(w.Id)).OrderBy(w => w.Name).ToListAsync(ct);

    public Task<Workspace?> GetAsync(Guid id, CancellationToken ct = default) =>
        db.Workspaces.FirstOrDefaultAsync(w => w.Id == id, ct);

    public Task<bool> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken ct = default) =>
        db.Workspaces.AnyAsync(w => w.Name == name && (excludeId == null || w.Id != excludeId), ct);

    public Task<bool> SlugExistsAsync(string slug, Guid? excludeId = null, CancellationToken ct = default) =>
        db.Workspaces.AnyAsync(w => w.Slug == slug && (excludeId == null || w.Id != excludeId), ct);

    public async Task<Workspace> CreateAsync(string name, string slug, string? description, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var workspace = new Workspace
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            Slug = slug,
            Description = description,
            IsDefault = false,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Workspaces.Add(workspace);
        await db.SaveChangesAsync(ct);
        return workspace;
    }

    public async Task<Workspace?> UpdateAsync(Guid id, string name, string slug, string? description, CancellationToken ct = default)
    {
        var workspace = await db.Workspaces.FirstOrDefaultAsync(w => w.Id == id, ct);
        if (workspace is null) return null;

        workspace.Name = name;
        workspace.Slug = slug;
        workspace.Description = description;
        workspace.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return workspace;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var workspace = await db.Workspaces.FirstOrDefaultAsync(w => w.Id == id, ct);
        if (workspace is null || workspace.IsDefault) return false;

        db.Workspaces.Remove(workspace);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<WorkspaceMembership>> GetMembershipsAsync(Guid workspaceId, CancellationToken ct = default) =>
        await db.WorkspaceMemberships
            .Include(m => m.User)
            .Where(m => m.WorkspaceId == workspaceId)
            .OrderBy(m => m.User.DisplayName)
            .ToListAsync(ct);

    public async Task SetMembershipAsync(Guid workspaceId, Guid userId, IReadOnlyList<string> roles, CancellationToken ct = default)
    {
        var membership = await db.WorkspaceMemberships
            .FirstOrDefaultAsync(m => m.WorkspaceId == workspaceId && m.UserId == userId, ct);

        var now = DateTime.UtcNow;
        if (membership is null)
        {
            membership = new WorkspaceMembership
            {
                Id = Guid.CreateVersion7(),
                WorkspaceId = workspaceId,
                UserId = userId,
                Roles = [.. roles],
                CreatedAt = now,
                UpdatedAt = now,
            };
            db.WorkspaceMemberships.Add(membership);
        }
        else
        {
            membership.Roles = [.. roles];
            membership.UpdatedAt = now;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> RemoveMembershipAsync(Guid workspaceId, Guid userId, CancellationToken ct = default)
    {
        var membership = await db.WorkspaceMemberships
            .FirstOrDefaultAsync(m => m.WorkspaceId == workspaceId && m.UserId == userId, ct);
        if (membership is null) return false;

        db.WorkspaceMemberships.Remove(membership);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
