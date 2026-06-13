using Datateal.Auth;
using Datateal.Core.Workspaces;
using Datateal.Ui.Server.Core.Repositories;
using Datateal.Ui.Shared.Workspaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Datateal.Ui.Server.Controllers;

/// <summary>
/// Tenant-level workspace administration and the per-user workspace list used by the
/// workspace switcher. Listing is available to any authenticated user (results are
/// filtered to the workspaces they can access); mutations require a tenant Admin, and
/// membership management additionally allows a WorkspaceAdmin of the target workspace.
/// </summary>
[ApiController]
[Route("api/workspaces")]
[Authorize]
public class WorkspacesController(
    IWorkspaceManagementRepository repository,
    IUserRepository users) : ControllerBase
{
    /// <summary>Workspaces the current user can access (Admin sees all).</summary>
    [HttpGet]
    public async Task<IReadOnlyList<WorkspaceDto>> GetAccessible(CancellationToken ct)
    {
        IReadOnlyList<Workspace> workspaces;
        if (User.IsInRole(DatatealRole.Admin))
        {
            workspaces = await repository.GetAllAsync(ct);
        }
        else
        {
            var ids = WorkspaceRoleClaims.GetWorkspaceIds(User).Distinct().ToList();
            workspaces = ids.Count == 0 ? [] : await repository.GetByIdsAsync(ids, ct);
        }

        return workspaces.Select(ToDto).ToList();
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        if (!CanAccess(id))
            return Forbid();

        var workspace = await repository.GetAsync(id, ct);
        return workspace is null ? NotFound() : Ok(ToDto(workspace));
    }

    [HttpPost]
    [Authorize(Policy = AuthPolicy.Admin)]
    public async Task<IActionResult> Create(CreateWorkspaceRequest body, CancellationToken ct)
    {
        if (await repository.NameExistsAsync(body.Name, ct: ct))
            return Conflict(Problem409("Name conflict", $"A workspace named '{body.Name}' already exists."));

        var workspace = await repository.CreateAsync(body.Name, body.Description, ct);
        return Created($"api/workspaces/{workspace.Id}", ToDto(workspace));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthPolicy.Admin)]
    public async Task<IActionResult> Update(Guid id, UpdateWorkspaceRequest body, CancellationToken ct)
    {
        if (await repository.NameExistsAsync(body.Name, id, ct))
            return Conflict(Problem409("Name conflict", $"A workspace named '{body.Name}' already exists."));

        var workspace = await repository.UpdateAsync(id, body.Name, body.Description, ct);
        return workspace is null ? NotFound() : Ok(ToDto(workspace));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthPolicy.Admin)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var workspace = await repository.GetAsync(id, ct);
        if (workspace is null) return NotFound();
        if (workspace.IsDefault)
            return BadRequest(Problem400("Cannot delete", "The default workspace cannot be deleted."));

        var deleted = await repository.DeleteAsync(id, ct);
        return deleted ? NoContent() : NotFound();
    }

    // ── Memberships ───────────────────────────────────────────────────────

    [HttpGet("{id:guid}/members")]
    public async Task<IActionResult> GetMembers(Guid id, CancellationToken ct)
    {
        if (!CanManageMembers(id))
            return Forbid();

        var memberships = await repository.GetMembershipsAsync(id, ct);
        var dtos = memberships
            .Select(m => new WorkspaceMemberDto(m.UserId, m.User.Email, m.User.DisplayName, m.Roles))
            .ToList();
        return Ok(dtos);
    }

    [HttpPut("{id:guid}/members")]
    public async Task<IActionResult> SetMember(Guid id, SetWorkspaceMemberRequest body, CancellationToken ct)
    {
        if (!CanManageMembers(id))
            return Forbid();

        var invalid = body.Roles.Where(r => !DatatealRole.IsPerWorkspace(r)).ToList();
        if (invalid.Count > 0)
            return BadRequest(Problem400("Invalid roles", $"Not valid per-workspace roles: {string.Join(", ", invalid)}."));

        if (await users.GetByIdAsync(body.UserId, ct) is null)
            return BadRequest(Problem400("Unknown user", "The specified user does not exist."));

        await repository.SetMembershipAsync(id, body.UserId, body.Roles, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid id, Guid userId, CancellationToken ct)
    {
        if (!CanManageMembers(id))
            return Forbid();

        var removed = await repository.RemoveMembershipAsync(id, userId, ct);
        return removed ? NoContent() : NotFound();
    }

    private bool CanAccess(Guid workspaceId) =>
        User.IsInRole(DatatealRole.Admin)
        || WorkspaceRoleClaims.GetWorkspaceIds(User).Contains(workspaceId);

    private bool CanManageMembers(Guid workspaceId) =>
        User.IsInRole(DatatealRole.Admin)
        || WorkspaceRoleClaims.GetRoles(User, workspaceId).Contains(DatatealRole.WorkspaceAdmin);

    private static WorkspaceDto ToDto(Workspace w) =>
        new(w.Id, w.Name, w.Description, w.IsDefault);

    private static ProblemDetails Problem400(string title, string detail) =>
        new() { Status = 400, Title = title, Detail = detail };

    private static ProblemDetails Problem409(string title, string detail) =>
        new() { Status = 409, Title = title, Detail = detail };
}
