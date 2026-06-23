using Datateal.Auth;
using Datateal.Core.Mediator;
using Datateal.Core.Nodes;
using Datateal.Ui.Server.Core.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Cmd = Datateal.Ui.Server.Application.Mediator.Commands;
using Qry = Datateal.Ui.Server.Application.Mediator.Queries;

namespace Datateal.Ui.Server.Controllers;

[ApiController]
[Authorize(Policy = AuthPolicy.NodePoolOperate)]
[Route("api/workspaces/{workspaceId:guid}/nodes")]
public class NodesController(IMediator mediator, IInteractivePoolRepository interactivePools) : ControllerBase
{
    /// <summary>
    /// Checks that <paramref name="nodeName"/> belongs to an interactive pool in
    /// <paramref name="workspaceId"/>. Returns a 403 result if not; <c>null</c> if ownership
    /// is confirmed and the caller should proceed.
    /// </summary>
    private async Task<IActionResult?> RequireNodeOwnershipAsync(Guid workspaceId, string nodeName, CancellationToken ct)
    {
        if (!await interactivePools.HasNodeAsync(workspaceId, nodeName, ct))
            return Forbid();
        return null;
    }

    [HttpGet]
    public async Task<IReadOnlyList<NodeInfo>> GetNodes(Guid workspaceId, CancellationToken ct)
    {
        var ownedNames = await interactivePools.GetNodeNamesAsync(workspaceId, ct);
        var nodes = await mediator.SendAsync(new Qry.GetNodesRequest(), ct);
        return nodes.Where(n => ownedNames.Contains(n.Name)).ToList();
    }

    [HttpGet("{name}")]
    public async Task<IActionResult> GetNode(Guid workspaceId, string name, CancellationToken ct)
    {
        if (await RequireNodeOwnershipAsync(workspaceId, name, ct) is { } deny) return deny;
        var node = await mediator.SendAsync(new Qry.GetNodeRequest(name), ct);
        return node is null ? NotFound() : Ok(node);
    }

    [HttpDelete("{name}")]
    public async Task<IActionResult> RemoveNode(Guid workspaceId, string name, CancellationToken ct)
    {
        if (await RequireNodeOwnershipAsync(workspaceId, name, ct) is { } deny) return deny;
        await mediator.SendAsync(new Cmd.RemoveNodeRequest(name), ct);
        return NoContent();
    }
}
