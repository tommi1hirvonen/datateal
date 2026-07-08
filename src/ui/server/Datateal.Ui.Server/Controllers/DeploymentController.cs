using Datateal.Auth;
using Datateal.Core.Mediator;
using Datateal.Deployment.Diff;
using Datateal.Deployment.Serialization;
using Datateal.Ui.Server.Application.Mediator.Commands;
using Datateal.Ui.Server.Application.Mediator.Queries;
using Datateal.Ui.Shared.Deployment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Datateal.Ui.Server.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class DeploymentController(IMediator mediator) : ControllerBase
{
    [HttpPost("deployments/admin/plan")]
    [Authorize(Policy = AuthPolicy.Admin)]
    public Task<ActionResult<ChangeSetDto>> PlanAdmin(CancellationToken ct) =>
        ExecuteChangeSetAsync(async () =>
        {
            var (bundle, env) = await ReadBundleAsync(ct);
            return await mediator.SendAsync(new PlanAdminDeploymentRequest(bundle, env), ct);
        });

    [HttpPost("deployments/admin/apply")]
    [Authorize(Policy = AuthPolicy.Admin)]
    public Task<ActionResult<ChangeSetDto>> ApplyAdmin(CancellationToken ct) =>
        ExecuteChangeSetAsync(async () =>
        {
            var (bundle, env) = await ReadBundleAsync(ct);
            return await mediator.SendAsync(new ApplyAdminDeploymentRequest(bundle, env), ct);
        });

    [HttpGet("deployments/admin/export")]
    [Authorize(Policy = AuthPolicy.Admin)]
    public async Task<IActionResult> ExportAdmin(CancellationToken ct) =>
        File(
            await mediator.SendAsync(new ExportAdminBundleRequest(), ct),
            "application/zip",
            "datateal-bundle.zip");

    [HttpPost("workspaces/{workspaceId:guid}/deployment/plan")]
    [Authorize(Policy = AuthPolicy.WorkspaceManage)]
    public Task<ActionResult<ChangeSetDto>> PlanWorkspace(Guid workspaceId, CancellationToken ct) =>
        ExecuteChangeSetAsync(async () =>
        {
            var (bundle, env) = await ReadBundleAsync(ct);
            var actingUserId = User.FindFirst(DatatealClaimTypes.UserId)?.Value;
            return await mediator.SendAsync(new PlanWorkspaceDeploymentRequest(workspaceId, bundle, actingUserId, env), ct);
        });

    [HttpPost("workspaces/{workspaceId:guid}/deployment/apply")]
    [Authorize(Policy = AuthPolicy.WorkspaceManage)]
    public Task<ActionResult<ChangeSetDto>> ApplyWorkspace(Guid workspaceId, CancellationToken ct) =>
        ExecuteChangeSetAsync(async () =>
        {
            var (bundle, env) = await ReadBundleAsync(ct);
            var actingUserId = User.FindFirst(DatatealClaimTypes.UserId)?.Value;
            return await mediator.SendAsync(new ApplyWorkspaceDeploymentRequest(workspaceId, bundle, actingUserId, env), ct);
        });

    [HttpGet("workspaces/{workspaceId:guid}/deployment/export")]
    [Authorize(Policy = AuthPolicy.WorkspaceManage)]
    public async Task<IActionResult> ExportWorkspace(Guid workspaceId, CancellationToken ct) =>
        File(
            await mediator.SendAsync(new ExportWorkspaceBundleRequest(workspaceId), ct),
            "application/zip",
            "datateal-bundle.zip");

    private async Task<(Bundle Bundle, IReadOnlyDictionary<string, string>? Env)> ReadBundleAsync(CancellationToken ct)
    {
        byte[] bytes;
        IReadOnlyDictionary<string, string>? env = null;

        if (Request.HasFormContentType)
        {
            var form = await Request.ReadFormAsync(ct);
            var file = form.Files.GetFile("bundle") ?? form.Files.FirstOrDefault();
            if (file is null)
                throw new InvalidOperationException("Bundle upload is missing the 'bundle' file.");

            await using var stream = file.OpenReadStream();
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, ct);
            bytes = ms.ToArray();

            if (form.TryGetValue("env", out var envJson) && !string.IsNullOrWhiteSpace(envJson))
            {
                env = JsonSerializer.Deserialize<Dictionary<string, string>>(envJson.ToString())
                    ?? throw new InvalidOperationException("The 'env' field must be a JSON object mapping variable names to string values.");
            }
        }
        else
        {
            using var ms = new MemoryStream();
            await Request.Body.CopyToAsync(ms, ct);
            bytes = ms.ToArray();
        }

        if (bytes.Length == 0)
            throw new InvalidOperationException("Bundle upload was empty.");

        return (BundleReader.ReadZip(bytes), env);
    }

    private static ChangeSetDto ToDto(ChangeSet changeSet) =>
        new(
            changeSet.Scope,
            changeSet.Target,
            changeSet.DryRun,
            changeSet.Changes
                .Select(change => new ResourceChangeDto(
                    change.ResourceType,
                    change.ResourceName,
                    change.ChangeType.ToString()))
                .ToList(),
            new ChangeSetSummaryDto(
                changeSet.Summary.Create,
                changeSet.Summary.Update,
                changeSet.Summary.Delete,
                changeSet.Summary.NoChange));

    private async Task<ActionResult<ChangeSetDto>> ExecuteChangeSetAsync(Func<Task<ChangeSet>> action)
    {
        try
        {
            return Ok(ToDto(await action()));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid deployment bundle",
                Detail = ex.Message,
            });
        }
    }
}
