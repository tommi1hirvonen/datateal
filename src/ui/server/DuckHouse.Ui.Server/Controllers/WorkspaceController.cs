using DuckHouse.Core.Mediator;
using DuckHouse.Ui.Server.Core.Catalogs;
using DuckHouse.Ui.Server.Core.Workspace;
using DuckHouse.Ui.Shared.Workspace;
using Microsoft.AspNetCore.Mvc;
using Cmd = DuckHouse.Ui.Server.Application.Mediator.Commands;
using Qry = DuckHouse.Ui.Server.Application.Mediator.Queries;
using SharedCat = DuckHouse.Ui.Shared.Catalogs;
using SharedWorkspace = DuckHouse.Ui.Shared.Workspace;

namespace DuckHouse.Ui.Server.Controllers;

[ApiController]
[Route("api/workspace")]
public class WorkspaceController(IMediator mediator) : ControllerBase
{
    [HttpGet("search")]
    public async Task<WorkspaceSearchResult> SearchItems([FromQuery] string? q, CancellationToken ct) =>
        await mediator.SendAsync(new Qry.SearchWorkspaceRequest(q ?? ""), ct);

    [HttpGet]
    public async Task<WorkspaceListing> GetRoot(CancellationToken ct) =>
        await mediator.SendAsync(new Qry.GetWorkspaceRequest(), ct);

    [HttpGet("folders/{id:guid}")]
    public async Task<WorkspaceListing> GetFolder(Guid id, CancellationToken ct) =>
        await mediator.SendAsync(new Qry.GetWorkspaceRequest(id), ct);

    [HttpGet("folders/{id:guid}/ancestors")]
    public async Task<IReadOnlyList<FolderSummary>> GetFolderAncestors(Guid id, CancellationToken ct) =>
        await mediator.SendAsync(new Qry.GetFolderAncestorsRequest(id), ct);

    [HttpPost("resolve")]
    public async Task<IActionResult> ResolvePath(SharedWorkspace.ResolvePathRequest body, CancellationToken ct)
    {
        var result = await mediator.SendAsync(new Qry.ResolveWorkspacePathRequest(body.Path, body.BaseFolderId), ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("folders")]
    public async Task<IActionResult> CreateFolder(SharedWorkspace.CreateFolderRequest body, CancellationToken ct)
    {
        try
        {
            var folder = await mediator.SendAsync(new Cmd.CreateFolderRequest(body.Name, body.ParentId), ct);
            return CreatedAtAction(nameof(GetFolder), new { id = folder.Id }, folder);
        }
        catch (WorkspaceNameValidationException ex)
        {
            return BadRequest(new ProblemDetails { Status = 400, Title = "Invalid name", Detail = ex.Message });
        }
    }

    [HttpPut("folders/{id:guid}")]
    public async Task<IActionResult> UpdateFolder(Guid id, SharedWorkspace.UpdateFolderRequest body, CancellationToken ct)
    {
        try
        {
            var folder = await mediator.SendAsync(new Cmd.UpdateFolderRequest(id, body.Name, body.ParentId), ct);
            return folder is null ? NotFound() : Ok(folder);
        }
        catch (WorkspaceNameValidationException ex)
        {
            return BadRequest(new ProblemDetails { Status = 400, Title = "Invalid name", Detail = ex.Message });
        }
    }

    [HttpDelete("folders/{id:guid}")]
    public async Task<IActionResult> DeleteFolder(Guid id, CancellationToken ct)
    {
        var found = await mediator.SendAsync(new Cmd.DeleteFolderRequest(id), ct);
        return found ? NoContent() : NotFound();
    }

    [HttpGet("notebooks/{id:guid}")]
    public async Task<IActionResult> GetNotebook(Guid id, CancellationToken ct)
    {
        var notebook = await mediator.SendAsync(new Qry.GetNotebookRequest(id), ct);
        return notebook is null ? NotFound() : Ok(notebook);
    }

    [HttpPost("notebooks")]
    public async Task<IActionResult> CreateNotebook(SharedWorkspace.CreateNotebookRequest body, CancellationToken ct)
    {
        try
        {
            var notebook = await mediator.SendAsync(new Cmd.CreateNotebookRequest(body.Title, body.Content, body.FolderId), ct);
            return CreatedAtAction(nameof(GetNotebook), new { id = notebook.Id }, notebook);
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
    public async Task<IActionResult> UpdateNotebook(Guid id, SharedWorkspace.UpdateNotebookRequest body, CancellationToken ct)
    {
        try
        {
            var notebook = await mediator.SendAsync(new Cmd.UpdateNotebookRequest(id, body.Title, body.Content, body.FolderId), ct);
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
    public async Task<IActionResult> DeleteNotebook(Guid id, CancellationToken ct)
    {
        var found = await mediator.SendAsync(new Cmd.DeleteNotebookRequest(id), ct);
        return found ? NoContent() : NotFound();
    }

    [HttpGet("queries/{id:guid}")]
    public async Task<IActionResult> GetQuery(Guid id, CancellationToken ct)
    {
        var query = await mediator.SendAsync(new Qry.GetQueryRequest(id), ct);
        return query is null ? NotFound() : Ok(query);
    }

    [HttpPost("queries")]
    public async Task<IActionResult> CreateQuery(SharedWorkspace.CreateQueryRequest body, CancellationToken ct)
    {
        try
        {
            var query = await mediator.SendAsync(new Cmd.CreateQueryRequest(body.Title, body.Content, body.FolderId), ct);
            return CreatedAtAction(nameof(GetQuery), new { id = query.Id }, query);
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
    public async Task<IActionResult> UpdateQuery(Guid id, SharedWorkspace.UpdateQueryRequest body, CancellationToken ct)
    {
        try
        {
            var query = await mediator.SendAsync(new Cmd.UpdateQueryRequest(id, body.Title, body.Content, body.FolderId), ct);
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
    public async Task<IActionResult> DeleteQuery(Guid id, CancellationToken ct)
    {
        var found = await mediator.SendAsync(new Cmd.DeleteQueryRequest(id), ct);
        return found ? NoContent() : NotFound();
    }

    [HttpPost("queries/{id:guid}/result")]
    public async Task<IActionResult> SaveQueryResult(Guid id, SharedWorkspace.SaveQueryResultRequest body, CancellationToken ct)
    {
        var found = await mediator.SendAsync(
            new Cmd.SaveQueryResultRequest(id, body.Status, body.DurationMs, body.DataFrame, body.Text, body.Error), ct);
        return found ? NoContent() : NotFound();
    }

    // ── Catalog associations ────────────────────────────────────────────

    [HttpGet("items/{id:guid}/catalogs")]
    public async Task<IActionResult> GetItemCatalogs(Guid id, CancellationToken ct)
    {
        // Re-use the notebook or query get to find the item
        var notebook = await mediator.SendAsync(new Qry.GetNotebookRequest(id), ct);
        if (notebook is not null) return Ok(notebook.CatalogNames ?? new List<string>());

        var query = await mediator.SendAsync(new Qry.GetQueryRequest(id), ct);
        if (query is not null) return Ok(query.CatalogNames ?? new List<string>());

        return NotFound();
    }

    [HttpPut("items/{id:guid}/catalogs")]
    public async Task<IActionResult> UpdateItemCatalogs(Guid id, SharedCat.UpdateWorkspaceItemCatalogsRequest body, CancellationToken ct)
    {
        try
        {
            var updated = await mediator.SendAsync(
                new Cmd.UpdateWorkspaceItemCatalogsRequest(id, body.CatalogNames), ct);
            return updated ? NoContent() : NotFound();
        }
        catch (CatalogNameConflictException ex)
        {
            return BadRequest(new ProblemDetails { Status = 400, Title = "Invalid catalog", Detail = ex.Message });
        }
    }
}
