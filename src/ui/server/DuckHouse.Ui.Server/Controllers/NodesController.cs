using DuckHouse.Auth;
using DuckHouse.Core.Nodes;
using DuckHouse.Core.Mediator;
using DuckHouse.Ui.Server.Core.Repositories;
using DuckHouse.Ui.Shared.Environment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Cmd = DuckHouse.Ui.Server.Application.Mediator.Commands;
using Qry = DuckHouse.Ui.Server.Application.Mediator.Queries;
using SharedNodes = DuckHouse.Ui.Shared.Nodes;

namespace DuckHouse.Ui.Server.Controllers;

[ApiController]
[Authorize(Policy = AuthPolicy.NodePoolOperate)]
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
        var nameError = ValidateNodeName(body.Name);
        if (nameError is not null)
            return BadRequest(nameError);

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

        // Resolve environment variables and secrets
        ResolvedEnvironment? resolved = null;
        if (body.EnvironmentVariableIds is { Count: > 0 } || body.SecretIds is { Count: > 0 })
        {
            resolved = await mediator.SendAsync(
                new Qry.ResolveEnvironmentRequest(body.EnvironmentVariableIds, body.SecretIds), ct);
        }

        var node = await mediator.SendAsync(
            new Cmd.CreateNodeRequest(
                body.Name, body.VmSize, body.KernelIdleTimeout, body.NodeIdleTimeout, body.KernelRequirements,
                wheelContents,
                resolved?.Variables,
                resolved?.Secrets), ct);
        return CreatedAtAction(nameof(GetNode), new { name = node.Name }, node);
    }

    [HttpDelete("{name}")]
    public async Task<IActionResult> RemoveNode(string name, CancellationToken ct)
    {
        await mediator.SendAsync(new Cmd.RemoveNodeRequest(name), ct);
        return NoContent();
    }

    private static string? ValidateNodeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Node name is required.";
        if (name.Length > 12)
            return "Node name must be 12 characters or fewer (AKS node pool name limit).";
        if (!System.Text.RegularExpressions.Regex.IsMatch(name, @"^[a-z][a-z0-9]{0,11}$"))
        {
            if (name != name.ToLowerInvariant())
                return "Node name must be lowercase.";
            if (!char.IsLetter(name[0]))
                return "Node name must start with a letter.";
            return "Node name may only contain lowercase letters and digits (no hyphens).";
        }
        return null;
    }
}
