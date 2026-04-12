using DuckHouse.Core.Mediator;
using DuckHouse.Ui.Server.Application.Mediator.Commands;
using DuckHouse.Ui.Server.Application.Mediator.Queries;
using DuckHouse.Ui.Server.Core.Repositories;
using DuckHouse.Ui.Shared.Packages;
using Microsoft.AspNetCore.Mvc;

namespace DuckHouse.Ui.Server.Controllers;

[ApiController]
[Route("api/packages")]
public class WheelPackagesController(IMediator mediator, IWheelPackageRepository repository) : ControllerBase
{
    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB

    [HttpGet]
    public async Task<IReadOnlyList<WheelPackageDto>> GetPackages(CancellationToken ct)
    {
        var packages = await mediator.SendAsync(new GetWheelPackagesRequest(), ct);
        return packages
            .Select(p => new WheelPackageDto(p.Id, p.Name, p.FileName, p.Size, p.UploadedAt))
            .ToList();
    }

    [HttpPost]
    [RequestSizeLimit(MaxFileSizeBytes + 1024)]
    public async Task<IActionResult> UploadPackage(IFormFile file, CancellationToken ct)
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
            var package = await mediator.SendAsync(new UploadWheelPackageRequest(name, file.FileName, data), ct);
            var dto = new WheelPackageDto(package.Id, package.Name, package.FileName, package.Size, package.UploadedAt);
            return CreatedAtAction(nameof(GetPackages), new { }, dto);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeletePackage(Guid id, CancellationToken ct)
    {
        var deleted = await mediator.SendAsync(new DeleteWheelPackageRequest(id), ct);
        return deleted ? NoContent() : NotFound();
    }

    [HttpGet("{id:guid}/{fileName}")]
    public async Task<IActionResult> DownloadPackage(Guid id, string fileName, CancellationToken ct)
    {
        var package = await repository.GetByIdAsync(id, ct);
        if (package is null) return NotFound();
        return File(package.Data, "application/octet-stream", package.FileName);
    }
}
