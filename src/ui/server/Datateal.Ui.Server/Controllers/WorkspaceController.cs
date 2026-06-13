using Datateal.Auth;
using Datateal.Core.Mediator;
using Datateal.Ui.Server.Core.Catalogs;
using Datateal.Ui.Server.Core.Workspace;
using Datateal.Ui.Shared.Workspace;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Cmd = Datateal.Ui.Server.Application.Mediator.Commands;
using Qry = Datateal.Ui.Server.Application.Mediator.Queries;
using SharedCat = Datateal.Ui.Shared.Catalogs;
using SharedWorkspace = Datateal.Ui.Shared.Workspace;

namespace Datateal.Ui.Server.Controllers;

[ApiController]
[Route("api/workspaces/{workspaceId:guid}/items")]
[Authorize(Policy = AuthPolicy.WorkspaceRead)]
public class WorkspaceController(IMediator mediator) : ControllerBase
{
    [HttpGet("search")]
    public async Task<WorkspaceSearchResult> SearchItems(Guid workspaceId, [FromQuery] string? q, CancellationToken ct) =>
        await mediator.SendAsync(new Qry.SearchWorkspaceRequest(workspaceId, q ?? ""), ct);

    [HttpGet]
    public async Task<WorkspaceListing> GetRoot(Guid workspaceId, CancellationToken ct) =>
        await mediator.SendAsync(new Qry.GetWorkspaceRequest(workspaceId), ct);

    [HttpGet("folders/{id:guid}")]
    public async Task<WorkspaceListing> GetFolder(Guid workspaceId, Guid id, CancellationToken ct) =>
        await mediator.SendAsync(new Qry.GetWorkspaceRequest(workspaceId, id), ct);

    [HttpGet("folders/{id:guid}/ancestors")]
    public async Task<IReadOnlyList<FolderSummary>> GetFolderAncestors(Guid workspaceId, Guid id, CancellationToken ct) =>
        await mediator.SendAsync(new Qry.GetFolderAncestorsRequest(workspaceId, id), ct);

    [HttpPost("resolve")]
    public async Task<IActionResult> ResolvePath(Guid workspaceId, SharedWorkspace.ResolvePathRequest body, CancellationToken ct)
    {
        var result = await mediator.SendAsync(new Qry.ResolveWorkspacePathRequest(workspaceId, body.Path, body.BaseFolderId), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("folders")]
    [Authorize(Policy = AuthPolicy.WorkspaceManage)]
    public async Task<IActionResult> CreateFolder(Guid workspaceId, SharedWorkspace.CreateFolderRequest body, CancellationToken ct)
    {
        try
        {
            var folder = await mediator.SendAsync(new Cmd.CreateFolderRequest(workspaceId, body.Name, body.ParentId), ct);
            return CreatedAtAction(nameof(GetFolder), new { workspaceId, id = folder.Id }, folder);
        }
        catch (WorkspaceNameValidationException ex)
        {
            return BadRequest(new ProblemDetails { Status = 400, Title = "Invalid name", Detail = ex.Message });
        }
    }

    [HttpPut("folders/{id:guid}")]
    [Authorize(Policy = AuthPolicy.WorkspaceManage)]
    public async Task<IActionResult> UpdateFolder(Guid workspaceId, Guid id, SharedWorkspace.UpdateFolderRequest body, CancellationToken ct)
    {
        try
        {
            var folder = await mediator.SendAsync(new Cmd.UpdateFolderRequest(workspaceId, id, body.Name, body.ParentId), ct);
            return folder is null ? NotFound() : Ok(folder);
        }
        catch (WorkspaceNameValidationException ex)
        {
            return BadRequest(new ProblemDetails { Status = 400, Title = "Invalid name", Detail = ex.Message });
        }
    }

    [HttpDelete("folders/{id:guid}")]
    [Authorize(Policy = AuthPolicy.WorkspaceManage)]
    public async Task<IActionResult> DeleteFolder(Guid workspaceId, Guid id, CancellationToken ct)
    {
        var found = await mediator.SendAsync(new Cmd.DeleteFolderRequest(workspaceId, id), ct);
        return found ? NoContent() : NotFound();
    }

    [HttpGet("notebooks/{id:guid}")]
    public async Task<IActionResult> GetNotebook(Guid workspaceId, Guid id, CancellationToken ct)
    {
        var notebook = await mediator.SendAsync(new Qry.GetNotebookRequest(workspaceId, id), ct);
        return notebook is null ? NotFound() : Ok(notebook);
    }

    [HttpPost("notebooks")]
    [Authorize(Policy = AuthPolicy.WorkspaceManage)]
    public async Task<IActionResult> CreateNotebook(Guid workspaceId, SharedWorkspace.CreateNotebookRequest body, CancellationToken ct)
    {
        try
        {
            var notebook = await mediator.SendAsync(new Cmd.CreateNotebookRequest(workspaceId, body.Title, body.Content, body.FolderId), ct);
            return CreatedAtAction(nameof(GetNotebook), new { workspaceId, id = notebook.Id }, notebook);
        }
        catch (WorkspaceNameValidationException ex)
        {
            return BadRequest(new ProblemDetails { Status = 400, Title = "Invalid name", Detail = ex.Message });
        }
        catch (WorkspaceTitleConflictException ex)
        {
            return Conflict(new ProblemDetails { Status = 409, Title = "Title conflict", Detail = ex.Message });
        }
    }

    [HttpPut("notebooks/{id:guid}")]
    [Authorize(Policy = AuthPolicy.WorkspaceManage)]
    public async Task<IActionResult> UpdateNotebook(Guid workspaceId, Guid id, SharedWorkspace.UpdateNotebookRequest body, CancellationToken ct)
    {
        try
        {
            var notebook = await mediator.SendAsync(new Cmd.UpdateNotebookRequest(workspaceId, id, body.Title, body.Content, body.FolderId), ct);
            return notebook is null ? NotFound() : Ok(notebook);
        }
        catch (WorkspaceNameValidationException ex)
        {
            return BadRequest(new ProblemDetails { Status = 400, Title = "Invalid name", Detail = ex.Message });
        }
        catch (WorkspaceTitleConflictException ex)
        {
            return Conflict(new ProblemDetails { Status = 409, Title = "Title conflict", Detail = ex.Message });
        }
    }

    [HttpDelete("notebooks/{id:guid}")]
    [Authorize(Policy = AuthPolicy.WorkspaceManage)]
    public async Task<IActionResult> DeleteNotebook(Guid workspaceId, Guid id, CancellationToken ct)
    {
        var found = await mediator.SendAsync(new Cmd.DeleteNotebookRequest(workspaceId, id), ct);
        return found ? NoContent() : NotFound();
    }

    [HttpGet("queries/{id:guid}")]
    public async Task<IActionResult> GetQuery(Guid workspaceId, Guid id, CancellationToken ct)
    {
        var query = await mediator.SendAsync(new Qry.GetQueryRequest(workspaceId, id), ct);
        return query is null ? NotFound() : Ok(query);
    }

    [HttpPost("queries")]
    [Authorize(Policy = AuthPolicy.WorkspaceManage)]
    public async Task<IActionResult> CreateQuery(Guid workspaceId, SharedWorkspace.CreateQueryRequest body, CancellationToken ct)
    {
        try
        {
            var query = await mediator.SendAsync(new Cmd.CreateQueryRequest(workspaceId, body.Title, body.Content, body.FolderId, body.LastResult), ct);
            return CreatedAtAction(nameof(GetQuery), new { workspaceId, id = query.Id }, query);
        }
        catch (WorkspaceNameValidationException ex)
        {
            return BadRequest(new ProblemDetails { Status = 400, Title = "Invalid name", Detail = ex.Message });
        }
        catch (WorkspaceTitleConflictException ex)
        {
            return Conflict(new ProblemDetails { Status = 409, Title = "Title conflict", Detail = ex.Message });
        }
    }

    [HttpPut("queries/{id:guid}")]
    [Authorize(Policy = AuthPolicy.WorkspaceManage)]
    public async Task<IActionResult> UpdateQuery(Guid workspaceId, Guid id, SharedWorkspace.UpdateQueryRequest body, CancellationToken ct)
    {
        try
        {
            var query = await mediator.SendAsync(new Cmd.UpdateQueryRequest(workspaceId, id, body.Title, body.Content, body.FolderId, body.LastResult), ct);
            return query is null ? NotFound() : Ok(query);
        }
        catch (WorkspaceNameValidationException ex)
        {
            return BadRequest(new ProblemDetails { Status = 400, Title = "Invalid name", Detail = ex.Message });
        }
        catch (WorkspaceTitleConflictException ex)
        {
            return Conflict(new ProblemDetails { Status = 409, Title = "Title conflict", Detail = ex.Message });
        }
    }

    [HttpDelete("queries/{id:guid}")]
    [Authorize(Policy = AuthPolicy.WorkspaceManage)]
    public async Task<IActionResult> DeleteQuery(Guid workspaceId, Guid id, CancellationToken ct)
    {
        var found = await mediator.SendAsync(new Cmd.DeleteQueryRequest(workspaceId, id), ct);
        return found ? NoContent() : NotFound();
    }

    // ── Catalog associations ────────────────────────────────────────────

    [HttpGet("{id:guid}/catalogs")]
    public async Task<IActionResult> GetItemCatalogs(Guid workspaceId, Guid id, CancellationToken ct)
    {
        // Re-use the notebook or query get to find the item
        var notebook = await mediator.SendAsync(new Qry.GetNotebookRequest(workspaceId, id), ct);
        if (notebook is not null) return Ok(notebook.CatalogNames ?? new List<string>());

        var query = await mediator.SendAsync(new Qry.GetQueryRequest(workspaceId, id), ct);
        if (query is not null) return Ok(query.CatalogNames ?? new List<string>());

        return NotFound();
    }

    [HttpPut("{id:guid}/catalogs")]
    [Authorize(Policy = AuthPolicy.WorkspaceManage)]
    public async Task<IActionResult> UpdateItemCatalogs(Guid workspaceId, Guid id, SharedCat.UpdateWorkspaceItemCatalogsRequest body, CancellationToken ct)
    {
        try
        {
            var updated = await mediator.SendAsync(
                new Cmd.UpdateWorkspaceItemCatalogsRequest(workspaceId, id, body.CatalogNames), ct);
            return updated ? NoContent() : NotFound();
        }
        catch (CatalogNameConflictException ex)
        {
            return BadRequest(new ProblemDetails { Status = 400, Title = "Invalid catalog", Detail = ex.Message });
        }
    }
}
