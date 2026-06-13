using Datateal.Auth;
using Datateal.Core.Mediator;
using Datateal.Ui.Shared.Environment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Cmd = Datateal.Ui.Server.Application.Mediator.Commands;
using Qry = Datateal.Ui.Server.Application.Mediator.Queries;
using SharedEnv = Datateal.Ui.Shared.Environment;

namespace Datateal.Ui.Server.Controllers;

[ApiController]
[Route("api/workspaces/{workspaceId:guid}/environment")]
[Authorize(Policy = AuthPolicy.EnvironmentManage)]
public class EnvironmentController(IMediator mediator) : ControllerBase
{
    // ── Variables ────────────────────────────────────────────────────────

    [HttpGet("variables")]
    public async Task<IReadOnlyList<EnvironmentVariableDto>> GetVariables(Guid workspaceId, CancellationToken ct) =>
        await mediator.SendAsync(new Qry.GetEnvironmentVariablesRequest(workspaceId), ct);

    [HttpPost("variables")]
    public async Task<IActionResult> CreateVariable(Guid workspaceId, SharedEnv.CreateEnvironmentVariableRequest body, CancellationToken ct)
    {
        var variable = await mediator.SendAsync(
            new Cmd.CreateEnvironmentVariableRequest(workspaceId, body.Key, body.Value, body.Description), ct);
        return Created($"api/workspaces/{workspaceId}/environment/variables/{variable.Id}", variable);
    }

    [HttpPut("variables/{id:guid}")]
    public async Task<IActionResult> UpdateVariable(Guid workspaceId, Guid id, SharedEnv.UpdateEnvironmentVariableRequest body, CancellationToken ct)
    {
        var variable = await mediator.SendAsync(
            new Cmd.UpdateEnvironmentVariableRequest(workspaceId, id, body.Key, body.Value, body.Description), ct);
        return variable is null ? NotFound() : Ok(variable);
    }

    [HttpDelete("variables/{id:guid}")]
    public async Task<IActionResult> DeleteVariable(Guid workspaceId, Guid id, CancellationToken ct)
    {
        var deleted = await mediator.SendAsync(new Cmd.DeleteEnvironmentVariableRequest(workspaceId, id), ct);
        return deleted ? NoContent() : NotFound();
    }

    // ── Secrets ──────────────────────────────────────────────────────────

    [HttpGet("secrets")]
    public async Task<IReadOnlyList<SecretDto>> GetSecrets(Guid workspaceId, CancellationToken ct) =>
        await mediator.SendAsync(new Qry.GetSecretsRequest(workspaceId), ct);

    [HttpPost("secrets")]
    public async Task<IActionResult> CreateSecret(Guid workspaceId, SharedEnv.CreateSecretRequest body, CancellationToken ct)
    {
        var secret = await mediator.SendAsync(
            new Cmd.CreateSecretRequest(workspaceId, body.Key, body.Value, body.Description), ct);
        return Created($"api/workspaces/{workspaceId}/environment/secrets/{secret.Id}", secret);
    }

    [HttpPut("secrets/{id:guid}")]
    public async Task<IActionResult> UpdateSecret(Guid workspaceId, Guid id, SharedEnv.UpdateSecretRequest body, CancellationToken ct)
    {
        var secret = await mediator.SendAsync(
            new Cmd.UpdateSecretRequest(workspaceId, id, body.Key, body.Value, body.Description), ct);
        return secret is null ? NotFound() : Ok(secret);
    }

    [HttpDelete("secrets/{id:guid}")]
    public async Task<IActionResult> DeleteSecret(Guid workspaceId, Guid id, CancellationToken ct)
    {
        var deleted = await mediator.SendAsync(new Cmd.DeleteSecretRequest(workspaceId, id), ct);
        return deleted ? NoContent() : NotFound();
    }

}
