using Datateal.Deployment.Diff;

namespace Datateal.Ui.Server.Application;

internal static class DeploymentChangeSetMerger
{
    public static ChangeSet Merge(ChangeSet primary, ChangeSet secondary) =>
        new()
        {
            Scope = primary.Scope,
            Target = primary.Target,
            DryRun = primary.DryRun,
            Changes = [.. primary.Changes, .. secondary.Changes],
        };
}
