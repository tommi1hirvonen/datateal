using Datateal.Auth;
using Datateal.Core.Mediator;
using Datateal.Ui.Server.Application.Mediator.Commands;
using Datateal.Ui.Server.Application.Mediator.Queries;
using Datateal.Ui.Server.Core.Repositories;
using Datateal.Ui.Shared.RuntimePackages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Datateal.Ui.Server.Controllers;

[ApiController]
[Route("api/workspaces/{workspaceId:guid}/packages")]
[Authorize(Policy = AuthPolicy.EnvironmentManage)]
public class WheelPackagesController(IMediator mediator, IWheelPackageRepository repository) : ControllerBase
{
    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB

    [HttpGet]
    public async Task<IReadOnlyList<WheelPackageDto>> GetPackages(Guid workspaceId, CancellationToken ct)
    {
        var packages = await mediator.SendAsync(new GetWheelPackagesRequest(workspaceId), ct);
        return packages
            .Select(p => new WheelPackageDto(p.Id, p.Name, p.FileName, p.Size, p.UploadedAt))
            .ToList();
    }

    [HttpPost]
    [RequestSizeLimit(MaxFileSizeBytes + 1024)]
    public async Task<IActionResult> UploadPackage(Guid workspaceId, IFormFile file, CancellationToken ct)
    {
        if (file.Length > MaxFileSizeBytes)
            return BadRequest($"File exceeds maximum size of {MaxFileSizeBytes / 1024 / 1024} MB.");

        if (!file.FileName.EndsWith(".whl", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Only .whl files are accepted.");

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        var data = ms.ToArray();

        // Use the original filename (without extension) as the package name
        var name = Path.GetFileNameWithoutExtension(file.FileName);

        try
        {
            var package = await mediator.SendAsync(new UploadWheelPackageRequest(workspaceId, name, file.FileName, data), ct);
            var dto = new WheelPackageDto(package.Id, package.Name, package.FileName, package.Size, package.UploadedAt);
            return CreatedAtAction(nameof(GetPackages), new { workspaceId }, dto);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeletePackage(Guid workspaceId, Guid id, CancellationToken ct)
    {
        var deleted = await mediator.SendAsync(new DeleteWheelPackageRequest(workspaceId, id), ct);
        return deleted ? NoContent() : NotFound();
    }

    [HttpGet("{id:guid}/{fileName}")]
    public async Task<IActionResult> DownloadPackage(Guid workspaceId, Guid id, string fileName, CancellationToken ct)
    {
        var package = await repository.GetByIdAsync(workspaceId, id, ct);
        if (package is null) return NotFound();
        return File(package.Data, "application/octet-stream", package.FileName);
    }
}
