using DuckHouse.Auth;
using DuckHouse.Core.Mediator;
using DuckHouse.Ui.Shared.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Cmd = DuckHouse.Ui.Server.Application.Mediator.Commands;
using Qry = DuckHouse.Ui.Server.Application.Mediator.Queries;

namespace DuckHouse.Ui.Server.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Policy = AuthPolicy.Admin)]
public class UsersController(IMediator mediator) : ControllerBase
{
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
        var user = await mediator.SendAsync(
            new Cmd.CreateUserCommand(body.Email, body.DisplayName, body.Roles, body.HasAllCatalogAccess, body.CatalogIds), ct);
        return Created($"api/users/{user.Id}", user);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateUserRequest body, CancellationToken ct)
    {
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
}
