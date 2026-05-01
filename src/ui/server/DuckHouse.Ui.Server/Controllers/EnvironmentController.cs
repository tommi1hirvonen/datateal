using DuckHouse.Auth;
using DuckHouse.Core.Mediator;
using DuckHouse.Ui.Shared.Environment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Cmd = DuckHouse.Ui.Server.Application.Mediator.Commands;
using Qry = DuckHouse.Ui.Server.Application.Mediator.Queries;
using SharedEnv = DuckHouse.Ui.Shared.Environment;

namespace DuckHouse.Ui.Server.Controllers;

[ApiController]
[Route("api/environment")]
[Authorize(Policy = AuthPolicy.EnvironmentManage)]
public class EnvironmentController(IMediator mediator) : ControllerBase
{
    // ── Variables ────────────────────────────────────────────────────────

    [HttpGet("variables")]
    public async Task<IReadOnlyList<EnvironmentVariableDto>> GetVariables(CancellationToken ct) =>
        await mediator.SendAsync(new Qry.GetEnvironmentVariablesRequest(), ct);

    [HttpPost("variables")]
    public async Task<IActionResult> CreateVariable(SharedEnv.CreateEnvironmentVariableRequest body, CancellationToken ct)
    {
        var variable = await mediator.SendAsync(
            new Cmd.CreateEnvironmentVariableRequest(body.Key, body.Value, body.Description), ct);
        return Created($"api/environment/variables/{variable.Id}", variable);
    }

    [HttpPut("variables/{id:guid}")]
    public async Task<IActionResult> UpdateVariable(Guid id, SharedEnv.UpdateEnvironmentVariableRequest body, CancellationToken ct)
    {
        var variable = await mediator.SendAsync(
            new Cmd.UpdateEnvironmentVariableRequest(id, body.Key, body.Value, body.Description), ct);
        return variable is null ? NotFound() : Ok(variable);
    }

    [HttpDelete("variables/{id:guid}")]
    public async Task<IActionResult> DeleteVariable(Guid id, CancellationToken ct)
    {
        var deleted = await mediator.SendAsync(new Cmd.DeleteEnvironmentVariableRequest(id), ct);
        return deleted ? NoContent() : NotFound();
    }

    // ── Secrets ──────────────────────────────────────────────────────────

    [HttpGet("secrets")]
    public async Task<IReadOnlyList<SecretDto>> GetSecrets(CancellationToken ct) =>
        await mediator.SendAsync(new Qry.GetSecretsRequest(), ct);

    [HttpPost("secrets")]
    public async Task<IActionResult> CreateSecret(SharedEnv.CreateSecretRequest body, CancellationToken ct)
    {
        var secret = await mediator.SendAsync(
            new Cmd.CreateSecretRequest(body.Key, body.Value, body.Description), ct);
        return Created($"api/environment/secrets/{secret.Id}", secret);
    }

    [HttpPut("secrets/{id:guid}")]
    public async Task<IActionResult> UpdateSecret(Guid id, SharedEnv.UpdateSecretRequest body, CancellationToken ct)
    {
        var secret = await mediator.SendAsync(
            new Cmd.UpdateSecretRequest(id, body.Key, body.Value, body.Description), ct);
        return secret is null ? NotFound() : Ok(secret);
    }

    [HttpDelete("secrets/{id:guid}")]
    public async Task<IActionResult> DeleteSecret(Guid id, CancellationToken ct)
    {
        var deleted = await mediator.SendAsync(new Cmd.DeleteSecretRequest(id), ct);
        return deleted ? NoContent() : NotFound();
    }

}
