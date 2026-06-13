using Datateal.Core.Workspaces;
using WorkspaceEntity = Datateal.Core.Workspaces.Workspace;

namespace Datateal.Ui.Server.Core.Repositories;

/// <summary>
/// Tenant-level management of workspaces and their memberships. These operations are not
/// scoped to an active workspace.
/// </summary>
public interface IWorkspaceManagementRepository
{
    Task<IReadOnlyList<WorkspaceEntity>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<WorkspaceEntity>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);
    Task<WorkspaceEntity?> GetAsync(Guid id, CancellationToken ct = default);
    Task<bool> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken ct = default);
    Task<WorkspaceEntity> CreateAsync(string name, string? description, CancellationToken ct = default);
    Task<WorkspaceEntity?> UpdateAsync(Guid id, string name, string? description, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<WorkspaceMembership>> GetMembershipsAsync(Guid workspaceId, CancellationToken ct = default);
    Task SetMembershipAsync(Guid workspaceId, Guid userId, IReadOnlyList<string> roles, CancellationToken ct = default);
    Task<bool> RemoveMembershipAsync(Guid workspaceId, Guid userId, CancellationToken ct = default);
}
