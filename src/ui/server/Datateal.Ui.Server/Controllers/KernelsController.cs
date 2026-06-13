using Datateal.Auth;
using Datateal.Core.Kernels;
using Datateal.Core.Mediator;
using Datateal.Ui.Server.Core.Catalogs;
using Datateal.Ui.Server.Core.Repositories;
using Datateal.Ui.Shared.Catalogs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Cmd = Datateal.Ui.Server.Application.Mediator.Commands;
using Qry = Datateal.Ui.Server.Application.Mediator.Queries;
using SharedKernels = Datateal.Ui.Shared.Kernels;

namespace Datateal.Ui.Server.Controllers;

[ApiController]
[Authorize(Policy = AuthPolicy.NodePoolOperate)]
[Route("api/workspaces/{workspaceId:guid}/nodes/{nodeName}/kernels")]
public class KernelsController(
    IMediator mediator,
    ICatalogAccessService catalogAccess,
    IInteractivePoolRepository interactivePools) : ControllerBase
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
    public async Task<IActionResult> GetKernels(Guid workspaceId, string nodeName, CancellationToken ct)
    {
        if (await RequireNodeOwnershipAsync(workspaceId, nodeName, ct) is { } deny) return deny;
        return Ok(await mediator.SendAsync(new Qry.GetKernelsRequest(nodeName), ct));
    }

    [HttpPost]
    public async Task<IActionResult> CreateKernel(Guid workspaceId, string nodeName, CancellationToken ct)
    {
        if (await RequireNodeOwnershipAsync(workspaceId, nodeName, ct) is { } deny) return deny;
        var kernel = await mediator.SendAsync(new Cmd.CreateKernelRequest(nodeName), ct);
        return CreatedAtAction(nameof(GetKernel), new { workspaceId, nodeName, kernelId = kernel.Id }, kernel);
    }

    [HttpGet("{kernelId}")]
    public async Task<IActionResult> GetKernel(Guid workspaceId, string nodeName, string kernelId, CancellationToken ct)
    {
        if (await RequireNodeOwnershipAsync(workspaceId, nodeName, ct) is { } deny) return deny;
        return Ok(await mediator.SendAsync(new Qry.GetKernelRequest(nodeName, kernelId), ct));
    }

    [HttpDelete("{kernelId}")]
    public async Task<IActionResult> DeleteKernel(Guid workspaceId, string nodeName, string kernelId, CancellationToken ct)
    {
        if (await RequireNodeOwnershipAsync(workspaceId, nodeName, ct) is { } deny) return deny;
        await mediator.SendAsync(new Cmd.DeleteKernelRequest(nodeName, kernelId), ct);
        return NoContent();
    }

    [HttpPost("{kernelId}/execute")]
    public async Task<IActionResult> Execute(Guid workspaceId, string nodeName, string kernelId, SharedKernels.ExecuteKernelRequest body, CancellationToken ct)
    {
        if (await RequireNodeOwnershipAsync(workspaceId, nodeName, ct) is { } deny) return deny;
        var handle = await mediator.SendAsync(new Cmd.ExecuteKernelRequest(nodeName, kernelId, body.Code, body.Timeout), ct);
        return Accepted(handle);
    }

    [HttpGet("{kernelId}/executions/{executionId}")]
    public async Task<IActionResult> PollExecution(Guid workspaceId, string nodeName, string kernelId, string executionId, CancellationToken ct)
    {
        if (await RequireNodeOwnershipAsync(workspaceId, nodeName, ct) is { } deny) return deny;
        return Ok(await mediator.SendAsync(new Qry.PollExecutionRequest(nodeName, kernelId, executionId), ct));
    }

    [HttpPost("{kernelId}/restart")]
    public async Task<IActionResult> RestartKernel(Guid workspaceId, string nodeName, string kernelId, CancellationToken ct)
    {
        if (await RequireNodeOwnershipAsync(workspaceId, nodeName, ct) is { } deny) return deny;
        return Ok(await mediator.SendAsync(new Cmd.RestartKernelRequest(nodeName, kernelId), ct));
    }

    [HttpPost("{kernelId}/interrupt")]
    public async Task<IActionResult> InterruptKernel(Guid workspaceId, string nodeName, string kernelId, CancellationToken ct)
    {
        if (await RequireNodeOwnershipAsync(workspaceId, nodeName, ct) is { } deny) return deny;
        await mediator.SendAsync(new Cmd.InterruptKernelRequest(nodeName, kernelId), ct);
        return NoContent();
    }

    [HttpPost("{kernelId}/completions")]
    public async Task<IActionResult> Complete(Guid workspaceId, string nodeName, string kernelId, [FromBody] CompleteRequest body, CancellationToken ct)
    {
        if (await RequireNodeOwnershipAsync(workspaceId, nodeName, ct) is { } deny) return deny;
        return Ok(await mediator.SendAsync(new Cmd.CompleteKernelRequest(nodeName, kernelId, body), ct));
    }

    [HttpPost("{kernelId}/diagnostics")]
    public async Task<IActionResult> Diagnose(Guid workspaceId, string nodeName, string kernelId, [FromBody] DiagnoseRequest body, CancellationToken ct)
    {
        if (await RequireNodeOwnershipAsync(workspaceId, nodeName, ct) is { } deny) return deny;
        return Ok(await mediator.SendAsync(new Cmd.DiagnoseKernelRequest(nodeName, kernelId, body), ct));
    }

    [HttpPost("{kernelId}/semantic-tokens")]
    public async Task<IActionResult> GetSemanticTokens(Guid workspaceId, string nodeName, string kernelId, [FromBody] SemanticTokenRequest body, CancellationToken ct)
    {
        if (await RequireNodeOwnershipAsync(workspaceId, nodeName, ct) is { } deny) return deny;
        return Ok(await mediator.SendAsync(new Cmd.GetSemanticTokensRequest(nodeName, kernelId, body), ct));
    }

    [HttpPost("{kernelId}/hover")]
    public async Task<IActionResult> GetHoverInfo(Guid workspaceId, string nodeName, string kernelId, [FromBody] HoverInfoRequest body, CancellationToken ct)
    {
        if (await RequireNodeOwnershipAsync(workspaceId, nodeName, ct) is { } deny) return deny;
        return Ok(await mediator.SendAsync(new Cmd.GetHoverInfoRequest(nodeName, kernelId, body), ct));
    }

    [HttpPost("{kernelId}/catalogs/setup")]
    public async Task<IActionResult> SetupCatalogs(Guid workspaceId, string nodeName, string kernelId, KernelCatalogSetupRequest body, CancellationToken ct)
    {
        if (await RequireNodeOwnershipAsync(workspaceId, nodeName, ct) is { } deny) return deny;
        var accessible = await catalogAccess.FilterAccessibleNamesAsync(User, workspaceId, body.CatalogNames, ct);
        if (accessible.Count < body.CatalogNames.Count)
            return Forbid();
        var handle = await mediator.SendAsync(new Cmd.SetupKernelCatalogsCommand(nodeName, kernelId, body.CatalogNames), ct);
        return Accepted(handle);
    }

    [HttpPost("{kernelId}/catalogs/{catalogName}/connect")]
    public async Task<IActionResult> ConnectCatalog(Guid workspaceId, string nodeName, string kernelId, string catalogName, CancellationToken ct)
    {
        if (await RequireNodeOwnershipAsync(workspaceId, nodeName, ct) is { } deny) return deny;
        if (!await catalogAccess.HasAccessByNameAsync(User, workspaceId, catalogName, ct))
            return Forbid();
        var handle = await mediator.SendAsync(new Cmd.ConnectKernelCatalogCommand(nodeName, kernelId, catalogName), ct);
        return Accepted(handle);
    }

    [HttpPost("{kernelId}/catalogs/{catalogName}/disconnect")]
    public async Task<IActionResult> DisconnectCatalog(Guid workspaceId, string nodeName, string kernelId, string catalogName, CancellationToken ct)
    {
        if (await RequireNodeOwnershipAsync(workspaceId, nodeName, ct) is { } deny) return deny;
        var handle = await mediator.SendAsync(new Cmd.DisconnectKernelCatalogCommand(nodeName, kernelId, catalogName), ct);
        return Accepted(handle);
    }
}
