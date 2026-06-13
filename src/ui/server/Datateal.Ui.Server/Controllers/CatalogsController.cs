using Datateal.Auth;
using Datateal.Core.Catalogs;
using Datateal.Core.Mediator;
using Datateal.Ui.Server.Core.Catalogs;
using Datateal.Ui.Server.Core.Repositories;
using Datateal.Ui.Shared.Catalogs;
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
    [HttpGet]
    public async Task<IReadOnlyList<CatalogDto>> GetAll(CancellationToken ct)
    {
        var catalogs = await mediator.SendAsync(new Qry.GetCatalogsRequest(), ct);
        var accessibleIds = await catalogAccess.GetAccessibleCatalogIdsAsync(User, ct);
        if (accessibleIds is null)
            return catalogs;
        return catalogs.Where(c => accessibleIds.Contains(c.Id)).ToList();
    }

    /// <summary>All catalogs, unfiltered. For tenant-level management (catalog access, user grants).</summary>
    [HttpGet("all")]
    [Authorize(Policy = AuthPolicy.CatalogManage)]
    public async Task<IReadOnlyList<CatalogDto>> GetAllUnfiltered(CancellationToken ct) =>
        await mediator.SendAsync(new Qry.GetCatalogsRequest(), ct);

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        if (!await catalogAccess.HasAccessAsync(User, id, ct))
            return Forbid();
        var catalogs = await mediator.SendAsync(new Qry.GetCatalogsRequest(), ct);
        var catalog = catalogs.FirstOrDefault(c => c.Id == id);
        return catalog is null ? NotFound() : Ok(catalog);
    }

    [HttpPost("managed")]
    [Authorize(Policy = AuthPolicy.CatalogManage)]
    public async Task<IActionResult> CreateManaged(SharedCat.CreateManagedCatalogRequest body, CancellationToken ct)
    {
        var catalog = await mediator.SendAsync(new Cmd.CreateManagedCatalogCommand(body.Name, body.AllowExistingDatabase), ct);
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
        var catalog = await mediator.SendAsync(new Cmd.UpdateManagedCatalogCommand(id, body.Name), ct);
        return catalog is null ? NotFound() : Ok(catalog);
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
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var deleted = await mediator.SendAsync(new Cmd.DeleteCatalogRequest(id), ct);
        return deleted ? NoContent() : NotFound();
    }

    [HttpGet("{id:guid}/metadata")]
    public async Task<IActionResult> GetMetadata(Guid id, CancellationToken ct)
    {
        if (!await catalogAccess.HasAccessAsync(User, id, ct))
            return Forbid();
        var metadata = await mediator.SendAsync(new Qry.GetCatalogMetadataRequest(id), ct);
        return metadata is null ? NotFound() : Ok(metadata);
    }

    [HttpGet("{id:guid}/info")]
    public async Task<IActionResult> GetInfo(Guid id, CancellationToken ct)
    {
        if (!await catalogAccess.HasAccessAsync(User, id, ct))
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
