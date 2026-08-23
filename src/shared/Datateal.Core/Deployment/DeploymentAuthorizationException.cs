namespace Datateal.Core.Deployment;

/// <summary>
/// Thrown when a workspace deployment plan/apply would create, update, or delete resources
/// (node pools, environment variables, secrets, wheel packages, or jobs) that require a
/// permission beyond the caller's baseline <c>WorkspaceManage</c> grant. The deployment bundle
/// endpoint is a single entry point that can touch many resource types with their own dedicated
/// RBAC policies elsewhere in the app (e.g. <c>NodePoolManage</c>, <c>EnvironmentManage</c>,
/// <c>JobManage</c>); this exception preserves that same per-resource-type authorization instead
/// of letting a coarse <c>WorkspaceManage</c> grant implicitly bypass it.
/// </summary>
public sealed class DeploymentAuthorizationException(string message) : Exception(message);
