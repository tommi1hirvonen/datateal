using Datateal.Auth;
using Datateal.Core.Nodes;
using Datateal.Core.Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Cmd = Datateal.Ui.Server.Application.Mediator.Commands;
using Qry = Datateal.Ui.Server.Application.Mediator.Queries;

namespace Datateal.Ui.Server.Controllers;

[ApiController]
[Authorize(Policy = AuthPolicy.NodePoolOperate)]
[Route("api/workspaces/{workspaceId:guid}/nodes")]
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
