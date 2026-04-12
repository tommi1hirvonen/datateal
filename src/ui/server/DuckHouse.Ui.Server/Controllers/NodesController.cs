using DuckHouse.Core.Nodes;
using DuckHouse.Core.Mediator;
using DuckHouse.Ui.Server.Core.Repositories;
using Microsoft.AspNetCore.Mvc;
using Cmd = DuckHouse.Ui.Server.Application.Mediator.Commands;
using Qry = DuckHouse.Ui.Server.Application.Mediator.Queries;
using SharedNodes = DuckHouse.Ui.Shared.Nodes;

namespace DuckHouse.Ui.Server.Controllers;

[ApiController]
[Route("api/nodes")]
public class NodesController(IMediator mediator, IWheelPackageRepository wheelPackageRepository) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<NodeInfo>> GetNodes(CancellationToken ct) =>
        await mediator.SendAsync(new Qry.GetNodesRequest(), ct);

    [HttpGet("{name}")]
    public async Task<IActionResult> GetNode(string name, CancellationToken ct)
    {
        var node = await mediator.SendAsync(new Qry.GetNodeRequest(name), ct);
        return node is null ? NotFound() : Ok(node);
    }

    [HttpPost]
    public async Task<IActionResult> CreateNode(SharedNodes.CreateNodeRequest body, CancellationToken ct)
    {
        IReadOnlyList<WheelContent>? wheelContents = null;
        if (body.WheelPackageIds is { Count: > 0 })
        {
            var packages = await wheelPackageRepository.GetByIdsAsync(body.WheelPackageIds, ct);
            var missing = body.WheelPackageIds.Except(packages.Select(p => p.Id)).ToList();
            if (missing.Count > 0)
                return BadRequest($"Wheel package(s) not found: {string.Join(", ", missing)}");

            wheelContents = packages
                .Select(p => new WheelContent(p.FileName, p.Data))
                .ToList();
        }

        var node = await mediator.SendAsync(
            new Cmd.CreateNodeRequest(body.Name, body.VmSize, body.KernelIdleTimeout, body.NodeIdleTimeout, body.KernelRequirements, wheelContents), ct);
        return CreatedAtAction(nameof(GetNode), new { name = node.Name }, node);
    }

    [HttpDelete("{name}")]
    public async Task<IActionResult> RemoveNode(string name, CancellationToken ct)
    {
        await mediator.SendAsync(new Cmd.RemoveNodeRequest(name), ct);
        return NoContent();
    }

    [HttpPost("{name}/stop")]
    public async Task<IActionResult> StopNode(string name, CancellationToken ct)
    {
        await mediator.SendAsync(new Cmd.StopNodeRequest(name), ct);
        return NoContent();
    }

    [HttpPost("{name}/start")]
    public async Task<IActionResult> StartNode(string name, CancellationToken ct)
    {
        await mediator.SendAsync(new Cmd.StartNodeRequest(name), ct);
        return NoContent();
    }
}
