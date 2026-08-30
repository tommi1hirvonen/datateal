using Datateal.Core.Workspaces;
using Datateal.Deployment.Diff;
using Datateal.Deployment.Models;

namespace Datateal.Ui.Server.Infrastructure.Deployment;

internal sealed class AdminMembershipMapper : IResourceMapper<WorkspaceMembershipModel>
{
    public string ResourceType => "workspace_membership";

    public WorkspaceMembershipModel ToModel(string workspaceName, IEnumerable<WorkspaceMembership> memberships) => new()
    {
        Workspace = workspaceName,
        Members = memberships
            .OrderBy(m => m.User.Email, StringComparer.OrdinalIgnoreCase)
            .Select(m => new WorkspaceMemberEntry
            {
                Email = m.User.Email,
                Roles = NormalizeRoles(m.Roles),
            })
            .ToList(),
    };

    public string NaturalKey(WorkspaceMembershipModel model) => model.Workspace.Trim();

    public bool AreEqual(WorkspaceMembershipModel desired, WorkspaceMembershipModel current)
    {
        if (!string.Equals(NaturalKey(desired), NaturalKey(current), StringComparison.OrdinalIgnoreCase))
            return false;

        var desiredByEmail = desired.Members.ToDictionary(m => m.Email, StringComparer.OrdinalIgnoreCase);
        var currentByEmail = current.Members.ToDictionary(m => m.Email, StringComparer.OrdinalIgnoreCase);

        if (desiredByEmail.Count != currentByEmail.Count)
            return false;

        foreach (var (email, desiredMember) in desiredByEmail)
        {
            if (!currentByEmail.TryGetValue(email, out var currentMember))
                return false;

            if (!NormalizeRoles(desiredMember.Roles).SequenceEqual(NormalizeRoles(currentMember.Roles), StringComparer.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Surfaces exactly which members gained access, lost access, or had their roles changed —
    /// an "Update" on the workspace as a whole would otherwise hide e.g. a user losing
    /// <c>WorkspaceAdmin</c> inside an unrelated addition of a different member.
    /// </summary>
    public List<FieldChange>? DiffDetails(WorkspaceMembershipModel desired, WorkspaceMembershipModel current)
    {
        var desiredByEmail = desired.Members.ToDictionary(m => m.Email, StringComparer.OrdinalIgnoreCase);
        var currentByEmail = current.Members.ToDictionary(m => m.Email, StringComparer.OrdinalIgnoreCase);
        var details = new List<FieldChange>();

        foreach (var (email, desiredMember) in desiredByEmail.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
        {
            var desiredRoles = string.Join(", ", NormalizeRoles(desiredMember.Roles));
            if (!currentByEmail.TryGetValue(email, out var currentMember))
            {
                details.Add(new FieldChange { Field = email, Before = "(none)", After = desiredRoles });
                continue;
            }

            var currentRoles = string.Join(", ", NormalizeRoles(currentMember.Roles));
            if (!string.Equals(desiredRoles, currentRoles, StringComparison.Ordinal))
                details.Add(new FieldChange { Field = email, Before = currentRoles, After = desiredRoles });
        }

        foreach (var (email, currentMember) in currentByEmail.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (!desiredByEmail.ContainsKey(email))
                details.Add(new FieldChange { Field = email, Before = string.Join(", ", NormalizeRoles(currentMember.Roles)), After = "(none)" });
        }

        return details.Count > 0 ? details : null;
    }

    private static List<string> NormalizeRoles(List<string>? roles) =>
        (roles ?? [])
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(role => role, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
