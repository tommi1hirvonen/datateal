using DuckHouse.Auth;
using DuckHouse.Core.Nodes;
using DuckHouse.Core.Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Cmd = DuckHouse.Ui.Server.Application.Mediator.Commands;
using Qry = DuckHouse.Ui.Server.Application.Mediator.Queries;

namespace DuckHouse.Ui.Server.Controllers;

[ApiController]
[Authorize(Policy = AuthPolicy.NodePoolOperate)]
[Route("api/nodes")]
public class NodesController(IMediator mediator) : ControllerBase
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

    [HttpDelete("{name}")]
    public async Task<IActionResult> RemoveNode(string name, CancellationToken ct)
    {
        await mediator.SendAsync(new Cmd.RemoveNodeRequest(name), ct);
        return NoContent();
    }
}
