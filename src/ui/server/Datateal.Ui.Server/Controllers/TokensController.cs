using Datateal.Auth;
using Datateal.Core.ApiTokens;
using Datateal.Core.Mediator;
using Datateal.Ui.Server.Auth;
using Datateal.Ui.Shared.ApiTokens;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Cmd = Datateal.Ui.Server.Application.Mediator.Commands;
using Qry = Datateal.Ui.Server.Application.Mediator.Queries;

namespace Datateal.Ui.Server.Controllers;

/// <summary>
/// Tenant-level management of API tokens. Admin-only. Tokens can be admin-scoped (tenant roles)
/// or workspace-scoped (per-workspace roles for a single workspace). The plaintext secret is
/// returned only once, on creation.
/// </summary>
[ApiController]
[Route("api/tokens")]
[Authorize(Policy = AuthPolicy.Admin)]
public class TokensController(IMediator mediator, IApiTokenAuthenticator authenticator) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<ApiTokenDto>> GetAll(CancellationToken ct) =>
        await mediator.SendAsync(new Qry.GetApiTokensRequest(), ct);

    [HttpPost]
    public async Task<IActionResult> Create(CreateApiTokenRequest body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Name))
            return Problem("Token name is required.", statusCode: StatusCodes.Status400BadRequest, title: "Invalid token");

        if (!Enum.TryParse<ApiTokenScopeType>(body.ScopeType, ignoreCase: true, out var scopeType))
            return Problem($"Unknown scope type '{body.ScopeType}'. Expected 'Admin' or 'Workspace'.",
                statusCode: StatusCodes.Status400BadRequest, title: "Invalid token");

        var roles = body.Roles ?? [];

        if (scopeType == ApiTokenScopeType.Workspace)
        {
            if (body.WorkspaceId is null)
                return Problem("A workspace-scoped token requires a workspace.",
                    statusCode: StatusCodes.Status400BadRequest, title: "Invalid token");

            var invalid = roles.Where(r => !DatatealRole.IsPerWorkspace(r)).ToList();
            if (invalid.Count > 0)
                return InvalidRoles(invalid, "per-workspace");
        }
        else
        {
            var invalid = roles.Where(r => !DatatealRole.IsTenantGlobal(r)).ToList();
            if (invalid.Count > 0)
                return InvalidRoles(invalid, "tenant-global");
        }

        if (body.ValidTo is { } validTo && validTo <= DateTime.UtcNow)
            return Problem("Expiry must be in the future.", statusCode: StatusCodes.Status400BadRequest, title: "Invalid token");

        var createdByUserId = GetActingUserId();

        var result = await mediator.SendAsync(
            new Cmd.CreateApiTokenCommand(
                body.Name.Trim(), scopeType, body.WorkspaceId, roles, body.ValidTo, createdByUserId), ct);

        return Created($"api/tokens/{result.Token.Id}", result);
    }

    [HttpPost("{id:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    {
        var revoked = await mediator.SendAsync(new Cmd.RevokeApiTokenCommand(id), ct);
        if (revoked)
            authenticator.Evict(id);
        return revoked ? NoContent() : NotFound();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var deleted = await mediator.SendAsync(new Cmd.DeleteApiTokenCommand(id), ct);
        if (deleted)
            authenticator.Evict(id);
        return deleted ? NoContent() : NotFound();
    }

    private Guid? GetActingUserId() =>
        Guid.TryParse(User.FindFirst(DatatealClaimTypes.UserId)?.Value, out var id) ? id : null;

    private ObjectResult InvalidRoles(IReadOnlyCollection<string> invalid, string expected) =>
        Problem(
            detail: $"Not valid {expected} roles: {string.Join(", ", invalid)}.",
            statusCode: StatusCodes.Status400BadRequest,
            title: "Invalid roles");
}
