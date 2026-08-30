namespace Datateal.Ui.Server.Application.Mediator.Commands;

/// <summary>
/// Captures which of the extra per-resource-type workspace policies the caller holds, resolved
/// server-side (in <c>DeploymentController</c>) via <c>IAuthorizationService</c> before a
/// workspace deployment plan/apply request reaches its handler. The baseline
/// <c>WorkspaceManage</c> policy is already enforced by the controller's <c>[Authorize]</c>
/// attribute, so it isn't tracked here.
/// </summary>
public sealed record WorkspaceDeploymentGrants(
    bool NodePoolManage,
    bool EnvironmentManage,
    bool JobManage);
