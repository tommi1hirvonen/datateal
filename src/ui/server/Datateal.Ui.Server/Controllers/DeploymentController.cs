using Datateal.Auth;
using Datateal.Core.Deployment;
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
public class DeploymentController(IMediator mediator, IAuthorizationService authorizationService) : ControllerBase
{
    // Bundles may embed multiple notebooks/queries plus wheel package binaries, so this is
    // generous compared to the single-file 5 MB cap enforced per wheel package (see
    // WheelPackageLimits), but still bounds the overall request body size.
    private const long MaxBundleSizeBytes = 50 * 1024 * 1024;

    [HttpPost("deployments/admin/plan")]
    [Authorize(Policy = AuthPolicy.Admin)]
    [RequestSizeLimit(MaxBundleSizeBytes)]
    public Task<ActionResult<ChangeSetDto>> PlanAdmin(CancellationToken ct) =>
        ExecuteChangeSetAsync(async () =>
        {
            var (bundle, env) = await ReadBundleAsync(ct);
            return await mediator.SendAsync(new PlanAdminDeploymentRequest(bundle, env), ct);
        });

    [HttpPost("deployments/admin/apply")]
    [Authorize(Policy = AuthPolicy.Admin)]
    [RequestSizeLimit(MaxBundleSizeBytes)]
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
    [RequestSizeLimit(MaxBundleSizeBytes)]
    public Task<ActionResult<ChangeSetDto>> PlanWorkspace(Guid workspaceId, CancellationToken ct) =>
        ExecuteChangeSetAsync(async () =>
        {
            var (bundle, env) = await ReadBundleAsync(ct);
            var actingUserId = User.FindFirst(DatatealClaimTypes.UserId)?.Value;
            var grants = await ResolveGrantsAsync(ct);
            return await mediator.SendAsync(new PlanWorkspaceDeploymentRequest(workspaceId, bundle, actingUserId, grants, env), ct);
        });

    [HttpPost("workspaces/{workspaceId:guid}/deployment/apply")]
    [Authorize(Policy = AuthPolicy.WorkspaceManage)]
    [RequestSizeLimit(MaxBundleSizeBytes)]
    public Task<ActionResult<ChangeSetDto>> ApplyWorkspace(Guid workspaceId, CancellationToken ct) =>
        ExecuteChangeSetAsync(async () =>
        {
            var (bundle, env) = await ReadBundleAsync(ct);
            var actingUserId = User.FindFirst(DatatealClaimTypes.UserId)?.Value;
            var grants = await ResolveGrantsAsync(ct);
            return await mediator.SendAsync(new ApplyWorkspaceDeploymentRequest(workspaceId, bundle, actingUserId, grants, env), ct);
        });

    [HttpGet("workspaces/{workspaceId:guid}/deployment/export")]
    [Authorize(Policy = AuthPolicy.WorkspaceManage)]
    [Authorize(Policy = AuthPolicy.NodePoolManage)]
    [Authorize(Policy = AuthPolicy.EnvironmentManage)]
    [Authorize(Policy = AuthPolicy.JobManage)]
    public async Task<IActionResult> ExportWorkspace(Guid workspaceId, CancellationToken ct) =>
        File(
            await mediator.SendAsync(new ExportWorkspaceBundleRequest(workspaceId), ct),
            "application/zip",
            "datateal-bundle.zip");

    // Export always returns the full workspace state (node pools, environment variables, secrets,
    // wheel packages, jobs) regardless of what's asked for, so it requires all four workspace
    // policies unconditionally — unlike Plan/Apply, whose extra requirements depend on what the
    // uploaded bundle would actually change (see ResolveGrantsAsync/DeploymentAuthorizationEvaluator).

    /// <summary>
    /// Resolves which of the extra per-resource-type workspace policies the caller holds, so
    /// Plan/Apply can enforce them against the bundle's actual computed changes once the bundle
    /// has been parsed. The baseline <see cref="AuthPolicy.WorkspaceManage"/> policy is already
    /// enforced by the action's <c>[Authorize]</c> attribute.
    /// </summary>
    private async Task<WorkspaceDeploymentGrants> ResolveGrantsAsync(CancellationToken ct)
    {
        var nodePoolManage = await authorizationService.AuthorizeAsync(User, null, AuthPolicy.NodePoolManage);
        var environmentManage = await authorizationService.AuthorizeAsync(User, null, AuthPolicy.EnvironmentManage);
        var jobManage = await authorizationService.AuthorizeAsync(User, null, AuthPolicy.JobManage);
        return new WorkspaceDeploymentGrants(
            nodePoolManage.Succeeded,
            environmentManage.Succeeded,
            jobManage.Succeeded);
    }

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
                try
                {
                    env = JsonSerializer.Deserialize<Dictionary<string, string>>(envJson.ToString())
                        ?? throw new InvalidOperationException("The 'env' field must be a JSON object mapping variable names to string values.");
                }
                catch (JsonException ex)
                {
                    throw new InvalidOperationException(
                        "The 'env' field must be a JSON object mapping variable names to string values.", ex);
                }
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
        catch (DeploymentConflictException ex)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Deployment already in progress",
                Detail = ex.Message,
            });
        }
        catch (DeploymentAuthorizationException ex)
        {
            return new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Insufficient permissions",
                Detail = ex.Message,
            })
            {
                StatusCode = StatusCodes.Status403Forbidden,
            };
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
