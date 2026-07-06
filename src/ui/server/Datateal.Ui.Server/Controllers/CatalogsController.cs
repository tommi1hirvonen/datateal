using Datateal.Auth;
using Datateal.Core.Catalogs;
using Datateal.Core.Mediator;
using Datateal.Ui.Server.Core.Catalogs;
using Datateal.Ui.Server.Core.Repositories;
using Datateal.Ui.Shared.Catalogs;
using Datateal.Ui.Shared.Workspaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Cmd = Datateal.Ui.Server.Application.Mediator.Commands;
using Qry = Datateal.Ui.Server.Application.Mediator.Queries;
using SharedCat = Datateal.Ui.Shared.Catalogs;

namespace Datateal.Ui.Server.Controllers;

[ApiController]
[Route("api/catalogs")]
[Authorize]
public class CatalogsController(
    IMediator mediator,
    ICatalogAccessService catalogAccess,
    ICatalogRepository catalogRepository) : ControllerBase
{
    /// <summary>
    /// The workspace-scoped read endpoints require an explicit workspace the caller can
    /// access. This prevents bypassing workspace-level catalog restrictions by omitting the
    /// id or supplying a workspace the user is not a member of: a malicious caller could
    /// otherwise read catalogs (and their metadata) that their active workspace is not granted.
    /// </summary>
    private bool CanAccessWorkspace(Guid workspaceId) =>
        User.IsInRole(DatatealRole.Admin)
        || WorkspaceRoleClaims.GetWorkspaceIds(User).Contains(workspaceId);

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? workspaceId = null, CancellationToken ct = default)
    {
        if (workspaceId is null || !CanAccessWorkspace(workspaceId.Value))
            return Forbid();

        var catalogs = await mediator.SendAsync(new Qry.GetCatalogsRequest(), ct);
        var accessibleIds = await catalogAccess.GetAccessibleCatalogIdsAsync(User, workspaceId, ct);
        if (accessibleIds is null)
            return Ok(catalogs);
        return Ok(catalogs.Where(c => accessibleIds.Contains(c.Id)).ToList());
    }

    /// <summary>All catalogs, unfiltered. For tenant-level management (catalog access, user grants).</summary>
    [HttpGet("all")]
    [Authorize(Policy = AuthPolicy.CatalogManage)]
    public async Task<IReadOnlyList<CatalogDto>> GetAllUnfiltered(CancellationToken ct) =>
        await mediator.SendAsync(new Qry.GetCatalogsRequest(), ct);

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, [FromQuery] Guid? workspaceId = null, CancellationToken ct = default)
    {
        if (workspaceId is null || !CanAccessWorkspace(workspaceId.Value))
            return Forbid();
        if (!await catalogAccess.HasAccessAsync(User, workspaceId, id, ct))
            return Forbid();
        var catalogs = await mediator.SendAsync(new Qry.GetCatalogsRequest(), ct);
        var catalog = catalogs.FirstOrDefault(c => c.Id == id);
        return catalog is null ? NotFound() : Ok(catalog);
    }

    [HttpPost("managed")]
    [Authorize(Policy = AuthPolicy.CatalogManage)]
    public async Task<IActionResult> CreateManaged(SharedCat.CreateManagedCatalogRequest body, CancellationToken ct)
    {
        var catalog = await mediator.SendAsync(
            new Cmd.CreateManagedCatalogCommand(body.Name, body.AllowExistingDatabase, body.ParquetV2, body.PerThreadOutput), ct);
        return Created($"api/catalogs/{catalog.Id}", catalog);
    }

    [HttpPost("unmanaged")]
    [Authorize(Policy = AuthPolicy.CatalogManage)]
    public async Task<IActionResult> CreateUnmanaged(SharedCat.CreateUnmanagedCatalogRequest body, CancellationToken ct)
    {
        var catalog = await mediator.SendAsync(
            new Cmd.CreateUnmanagedCatalogCommand(
                body.Name, body.DataPath, body.StorageConnectionString,
                body.CatalogHost, body.CatalogPort, body.CatalogDatabase,
                body.CatalogUser, body.CatalogPassword), ct);
        return Created($"api/catalogs/{catalog.Id}", catalog);
    }

    [HttpPut("{id:guid}/managed")]
    [Authorize(Policy = AuthPolicy.CatalogManage)]
    public async Task<IActionResult> UpdateManaged(Guid id, SharedCat.UpdateManagedCatalogRequest body, CancellationToken ct)
    {
        var catalog = await mediator.SendAsync(
            new Cmd.UpdateManagedCatalogCommand(id, body.Name, body.ParquetV2, body.PerThreadOutput), ct);
        return catalog is null ? NotFound() : Ok(catalog);
    }

    [HttpGet("{id:guid}/managed/ducklake-settings")]
    [Authorize(Policy = AuthPolicy.CatalogManage)]
    public async Task<IActionResult> GetManagedDuckLakeSettings(Guid id, CancellationToken ct)
    {
        var settings = await mediator.SendAsync(new Qry.GetManagedCatalogDuckLakeSettingsRequest(id), ct);
        return settings is null ? StatusCode(503) : Ok(settings);
    }

    [HttpPut("{id:guid}/unmanaged")]
    [Authorize(Policy = AuthPolicy.CatalogManage)]
    public async Task<IActionResult> UpdateUnmanaged(Guid id, SharedCat.UpdateUnmanagedCatalogRequest body, CancellationToken ct)
    {
        var catalog = await mediator.SendAsync(
            new Cmd.UpdateUnmanagedCatalogCommand(
                id, body.Name, body.DataPath, body.StorageConnectionString,
                body.CatalogHost, body.CatalogPort, body.CatalogDatabase,
                body.CatalogUser, body.CatalogPassword), ct);
        return catalog is null ? NotFound() : Ok(catalog);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthPolicy.CatalogManage)]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] bool dropDatabase, CancellationToken ct = default)
    {
        var deleted = await mediator.SendAsync(new Cmd.DeleteCatalogRequest(id, dropDatabase), ct);
        return deleted ? NoContent() : NotFound();
    }

    [HttpGet("{id:guid}/metadata")]
    public async Task<IActionResult> GetMetadata(Guid id, [FromQuery] Guid? workspaceId = null, CancellationToken ct = default)
    {
        if (workspaceId is null || !CanAccessWorkspace(workspaceId.Value))
            return Forbid();
        if (!await catalogAccess.HasAccessAsync(User, workspaceId, id, ct))
            return Forbid();
        var metadata = await mediator.SendAsync(new Qry.GetCatalogMetadataRequest(id), ct);
        return metadata is null ? NotFound() : Ok(metadata);
    }

    [HttpGet("{id:guid}/info")]
    public async Task<IActionResult> GetInfo(Guid id, [FromQuery] Guid? workspaceId = null, CancellationToken ct = default)
    {
        if (workspaceId is null || !CanAccessWorkspace(workspaceId.Value))
            return Forbid();
        if (!await catalogAccess.HasAccessAsync(User, workspaceId, id, ct))
            return Forbid();
        var info = await mediator.SendAsync(new Qry.GetCatalogInfoRequest(id), ct);
        return info is null ? NotFound() : Ok(info);
    }

    [HttpGet("{id:guid}/workspace-access")]
    [Authorize(Policy = AuthPolicy.CatalogManage)]
    public async Task<IActionResult> GetWorkspaceAccess(Guid id, CancellationToken ct)
    {
        var access = await catalogRepository.GetWorkspaceAccessAsync(id, ct);
        return access is null
            ? NotFound()
            : Ok(new SharedCat.CatalogWorkspaceAccessDto(access.Value.AccessibleFromAllWorkspaces, access.Value.WorkspaceIds));
    }

    [HttpPut("{id:guid}/workspace-access")]
    [Authorize(Policy = AuthPolicy.CatalogManage)]
    public async Task<IActionResult> SetWorkspaceAccess(Guid id, SharedCat.SetCatalogWorkspaceAccessRequest body, CancellationToken ct)
    {
        var updated = await catalogRepository.SetWorkspaceAccessAsync(
            id, body.AccessibleFromAllWorkspaces, body.WorkspaceIds, ct);
        return updated ? NoContent() : NotFound();
    }
}
