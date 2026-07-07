using Datateal.Auth;
using Datateal.Core.Mediator;
using Datateal.Ui.Shared.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Cmd = Datateal.Ui.Server.Application.Mediator.Commands;
using Qry = Datateal.Ui.Server.Application.Mediator.Queries;

namespace Datateal.Ui.Server.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Policy = AuthPolicy.Admin)]
public class UsersController(IMediator mediator) : ControllerBase
{
    // ── User accounts ─────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IReadOnlyList<AppUserDto>> GetAll(CancellationToken ct) =>
        await mediator.SendAsync(new Qry.GetUsersRequest(), ct);

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var user = await mediator.SendAsync(new Qry.GetUserRequest(id), ct);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateUserRequest body, CancellationToken ct)
    {
        if (InvalidTenantRoles(body.Roles) is { } error)
            return error;

        var user = await mediator.SendAsync(
            new Cmd.CreateUserCommand(body.Email, body.DisplayName, body.Roles, body.HasAllCatalogAccess, body.CatalogIds), ct);
        return Created($"api/users/{user.Id}", user);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateUserRequest body, CancellationToken ct)
    {
        if (InvalidTenantRoles(body.Roles) is { } error)
            return error;

        var user = await mediator.SendAsync(
            new Cmd.UpdateUserCommand(id, body.DisplayName, body.IsEnabled, body.Roles, body.HasAllCatalogAccess, body.CatalogIds), ct);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var deleted = await mediator.SendAsync(new Cmd.DeleteUserCommand(id), ct);
        return deleted ? NoContent() : NotFound();
    }

    // ── Service accounts ──────────────────────────────────────────────────

    [HttpGet("service-accounts")]
    public async Task<IReadOnlyList<ServiceAccountDto>> GetAllServiceAccounts(CancellationToken ct) =>
        await mediator.SendAsync(new Qry.GetServiceAccountsRequest(), ct);

    [HttpPost("service-accounts")]
    public async Task<IActionResult> CreateServiceAccount(CreateServiceAccountRequest body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Name))
            return Problem("Name is required.", statusCode: StatusCodes.Status400BadRequest, title: "Invalid service account");

        if (InvalidTenantRoles(body.Roles) is { } error)
            return error;

        var account = await mediator.SendAsync(
            new Cmd.CreateServiceAccountCommand(body.Name.Trim(), body.Description, body.Roles, body.HasAllCatalogAccess, body.CatalogIds), ct);
        return Created($"api/users/service-accounts/{account.Id}", account);
    }

    [HttpPut("service-accounts/{id:guid}")]
    public async Task<IActionResult> UpdateServiceAccount(Guid id, UpdateServiceAccountRequest body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Name))
            return Problem("Name is required.", statusCode: StatusCodes.Status400BadRequest, title: "Invalid service account");

        if (InvalidTenantRoles(body.Roles) is { } error)
            return error;

        var account = await mediator.SendAsync(
            new Cmd.UpdateServiceAccountCommand(id, body.Name.Trim(), body.Description, body.IsEnabled, body.Roles, body.HasAllCatalogAccess, body.CatalogIds), ct);
        return account is null ? NotFound() : Ok(account);
    }

    [HttpDelete("service-accounts/{id:guid}")]
    public async Task<IActionResult> DeleteServiceAccount(Guid id, CancellationToken ct)
    {
        var deleted = await mediator.SendAsync(new Cmd.DeleteServiceAccountCommand(id), ct);
        return deleted ? NoContent() : NotFound();
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    /// <summary>
    /// The tenant role store (<c>AppUser.Roles</c>) may only hold tenant-global roles.
    /// Per-workspace roles are granted via workspace memberships, so reject them here to
    /// keep the role store clean and unambiguous.
    /// </summary>
    private BadRequestObjectResult? InvalidTenantRoles(IEnumerable<string> roles)
    {
        var invalid = roles.Where(r => !DatatealRole.IsTenantGlobal(r)).ToList();
        return invalid.Count == 0
            ? null
            : BadRequest(new ProblemDetails
            {
                Status = 400,
                Title = "Invalid roles",
                Detail = $"Not valid tenant-global roles: {string.Join(", ", invalid)}. " +
                         "Per-workspace roles are assigned from a workspace's members list.",
            });
    }
}
