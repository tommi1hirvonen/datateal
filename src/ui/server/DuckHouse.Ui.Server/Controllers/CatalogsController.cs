using DuckHouse.Core.Mediator;
using DuckHouse.Ui.Shared.Catalogs;
using Microsoft.AspNetCore.Mvc;
using Cmd = DuckHouse.Ui.Server.Application.Mediator.Commands;
using Qry = DuckHouse.Ui.Server.Application.Mediator.Queries;
using SharedCat = DuckHouse.Ui.Shared.Catalogs;

namespace DuckHouse.Ui.Server.Controllers;

[ApiController]
[Route("api/catalogs")]
public class CatalogsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<CatalogDto>> GetAll(CancellationToken ct) =>
        await mediator.SendAsync(new Qry.GetCatalogsRequest(), ct);

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var catalogs = await mediator.SendAsync(new Qry.GetCatalogsRequest(), ct);
        var catalog = catalogs.FirstOrDefault(c => c.Id == id);
        return catalog is null ? NotFound() : Ok(catalog);
    }

    [HttpPost]
    public async Task<IActionResult> Create(SharedCat.CreateCatalogRequest body, CancellationToken ct)
    {
        var catalog = await mediator.SendAsync(
            new Cmd.CreateCatalogRequest(
                body.Name, body.IsManaged, body.DataPath, body.StorageConnectionString,
                body.CatalogHost, body.CatalogPort, body.CatalogDatabase,
                body.CatalogUser, body.CatalogPassword), ct);
        return Created($"api/catalogs/{catalog.Id}", catalog);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, SharedCat.UpdateCatalogRequest body, CancellationToken ct)
    {
        var catalog = await mediator.SendAsync(
            new Cmd.UpdateCatalogRequest(
                id, body.Name, body.DataPath, body.StorageConnectionString,
                body.CatalogHost, body.CatalogPort, body.CatalogDatabase,
                body.CatalogUser, body.CatalogPassword), ct);
        return catalog is null ? NotFound() : Ok(catalog);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var deleted = await mediator.SendAsync(new Cmd.DeleteCatalogRequest(id), ct);
        return deleted ? NoContent() : NotFound();
    }

    [HttpGet("{id:guid}/metadata")]
    public async Task<IActionResult> GetMetadata(Guid id, CancellationToken ct)
    {
        var metadata = await mediator.SendAsync(new Qry.GetCatalogMetadataRequest(id), ct);
        return metadata is null ? NotFound() : Ok(metadata);
    }

    [HttpPost("resolve")]
    public async Task<IReadOnlyList<ResolvedCatalogDto>> Resolve(SharedCat.ResolveCatalogsRequest body, CancellationToken ct) =>
        await mediator.SendAsync(new Qry.ResolveCatalogsRequest(body.CatalogNames), ct);
}
