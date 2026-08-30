using System.Runtime.CompilerServices;
using Datateal.Core.Deployment;
using Datateal.Deployment.Diff;

[assembly: InternalsVisibleTo("Datateal.Core.Tests")]

namespace Datateal.Ui.Server.Application.Mediator.Commands;

/// <summary>
/// Enforces that a workspace deployment plan/apply only touches resource types the caller is
/// authorized to manage. The deployment bundle endpoint is a single entry point that can create,
/// update, or delete node pools, environment variables, secrets, wheel packages, and orchestrator
/// jobs — resources that each have their own dedicated RBAC policy everywhere else in the app
/// (<c>NodePoolManage</c>, <c>EnvironmentManage</c>, <c>JobManage</c>). This evaluator inspects
/// the already-computed change set (not just the raw bundle contents) so it correctly accounts
/// for implicit deletions of resources omitted from the bundle, and so a caller isn't required to
/// hold extra permissions for a bundle that merely re-declares state with no actual changes.
/// </summary>
internal static class DeploymentAuthorizationEvaluator
{
    private static readonly HashSet<string> EnvironmentResourceTypes =
        new(["environment_variable", "secret", "wheel_package"], StringComparer.Ordinal);

    public static void EnsureAuthorized(
        ChangeSet workspaceChanges,
        ChangeSet? jobChanges,
        WorkspaceDeploymentGrants grants)
    {
        var missing = new List<string>();

        var hasNodePoolChanges = workspaceChanges.Changes.Any(
            c => c.ResourceType == "node_pool" && c.ChangeType != ChangeType.NoChange);
        if (hasNodePoolChanges && !grants.NodePoolManage)
            missing.Add("NodePoolManage (this deployment creates, updates, or deletes node pools)");

        var hasEnvironmentChanges = workspaceChanges.Changes.Any(
            c => EnvironmentResourceTypes.Contains(c.ResourceType) && c.ChangeType != ChangeType.NoChange);
        if (hasEnvironmentChanges && !grants.EnvironmentManage)
            missing.Add("EnvironmentManage (this deployment creates, updates, or deletes environment variables, secrets, or wheel packages)");

        var hasJobChanges = jobChanges is not null
            && jobChanges.Changes.Any(c => c.ChangeType != ChangeType.NoChange);
        if (hasJobChanges && !grants.JobManage)
            missing.Add("JobManage (this deployment creates, updates, or deletes jobs)");

        if (missing.Count > 0)
        {
            throw new DeploymentAuthorizationException(
                "This deployment requires additional permissions you don't have: " +
                string.Join("; ", missing) + ".");
        }
    }
}
