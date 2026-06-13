using Datateal.Ui.Shared.Workspaces;

namespace Datateal.Ui.Client.Services;

public interface IWorkspaceManagementService
{
    Task<IReadOnlyList<WorkspaceDto>> GetAccessibleAsync(CancellationToken ct = default);
    Task<WorkspaceDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<WorkspaceDto> CreateAsync(CreateWorkspaceRequest request, CancellationToken ct = default);
    Task<WorkspaceDto?> UpdateAsync(Guid id, UpdateWorkspaceRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<WorkspaceMemberDto>> GetMembersAsync(Guid workspaceId, CancellationToken ct = default);
    Task SetMemberAsync(Guid workspaceId, SetWorkspaceMemberRequest request, CancellationToken ct = default);
    Task RemoveMemberAsync(Guid workspaceId, Guid userId, CancellationToken ct = default);
}
