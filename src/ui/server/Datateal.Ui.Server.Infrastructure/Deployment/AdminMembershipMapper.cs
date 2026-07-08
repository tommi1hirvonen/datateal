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

        var currentByEmail = current.Members.ToDictionary(m => m.Email, StringComparer.OrdinalIgnoreCase);
        foreach (var desiredMember in desired.Members)
        {
            if (!currentByEmail.TryGetValue(desiredMember.Email, out var currentMember))
                return false;

            if (!NormalizeRoles(desiredMember.Roles).SequenceEqual(NormalizeRoles(currentMember.Roles), StringComparer.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    private static List<string> NormalizeRoles(List<string>? roles) =>
        (roles ?? [])
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(role => role, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
