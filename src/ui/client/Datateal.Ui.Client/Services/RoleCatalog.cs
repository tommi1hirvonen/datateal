using Datateal.Auth;

namespace Datateal.Ui.Client.Services;

/// <summary>
/// Human-readable descriptions for the application roles, surfaced in the role pickers'
/// info popovers. Descriptions disambiguate <b>tenant-global</b> roles (assigned on the
/// Users page and effective across the whole instance) from <b>per-workspace</b> roles
/// (assigned on a workspace's members list and effective only within that workspace).
/// </summary>
public static class RoleCatalog
{
    public static readonly IReadOnlyDictionary<string, string> Descriptions = new Dictionary<string, string>
    {
        // ── Tenant-global roles (stored on the user, apply across every workspace) ──
        [DatatealRole.Admin] =
            "Tenant administrator. Full access across the whole instance: manage users, " +
            "create and manage every workspace, and access all catalogs. Implicitly has " +
            "every role in every workspace.",
        [DatatealRole.CatalogContributor] =
            "Tenant-wide catalog manager. Create, edit, and delete catalogs and schemas " +
            "across the instance, with implicit access to all catalog data. Applies to the " +
            "whole tenant, not a single workspace.",

        // ── Per-workspace roles (stored on the membership, apply only in that workspace) ──
        [DatatealRole.WorkspaceAdmin] =
            "Administrator of this workspace. Manage the workspace's members and their roles, " +
            "and do everything every other per-workspace role allows within this workspace. " +
            "Does not grant tenant-level administration or access to other workspaces.",
        [DatatealRole.WorkspaceContributor] =
            "Create, edit, and delete content (folders, notebooks, queries) in this workspace. " +
            "Does not grant kernel or code execution — also requires NodePoolOperator for that.",
        [DatatealRole.WorkspaceReader] =
            "Browse and read content in this workspace. Cannot make changes. Also requires " +
            "NodePoolOperator to connect to kernels and execute code.",
        [DatatealRole.NodePoolContributor] =
            "Create, edit, and delete node pool configurations in this workspace, and start/stop " +
            "pools, create kernel sessions, and execute code in notebooks and queries.",
        [DatatealRole.NodePoolOperator] =
            "Start and stop interactive node pools in this workspace, create kernel sessions, and " +
            "execute code in notebooks and queries. Cannot change pool configurations.",
        [DatatealRole.JobContributor] =
            "Create, edit, and delete job definitions in this workspace.",
        [DatatealRole.JobOperator] =
            "Run, monitor, and cancel jobs in this workspace. Cannot create or modify job definitions.",
        [DatatealRole.JobReader] =
            "View and monitor jobs in this workspace. Cannot run, cancel, or modify jobs.",
        [DatatealRole.EnvironmentManager] =
            "Manage this workspace's environment variables, secrets, and packages.",
    };

    public static string Describe(string role) => Descriptions.GetValueOrDefault(role, role);
}
