using Datateal.Auth;
using Datateal.Core.Mediator;
using Datateal.Ui.Shared.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Cmd = Datateal.Ui.Server.Application.Mediator.Commands;
using Qry = Datateal.Ui.Server.Application.Mediator.Queries;

namespace Datateal.Ui.Server.Controllers;

[ApiController]
[Authorize(Policy = AuthPolicy.NodePoolOperate)]
[Route("api/workspaces/{workspaceId:guid}/interactive-pools")]
public class InteractivePoolsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<InteractivePoolDto>> GetInteractivePools(Guid workspaceId, CancellationToken ct) =>
        await mediator.SendAsync(new Qry.GetInteractivePoolsRequest(workspaceId), ct);

    [HttpPost("{name}/ensure-node")]
    public async Task<IActionResult> EnsureNode(Guid workspaceId, string name, CancellationToken ct)
    {
        var node = await mediator.SendAsync(new Cmd.EnsureInteractiveNodeRequest(workspaceId, name), ct);
        return node is null ? NotFound() : Ok(node);
    }
}
