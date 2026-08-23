using Datateal.Deployment.Models;
using Datateal.Ui.Server.Infrastructure.Deployment;

namespace Datateal.Core.Tests.Deployment;

/// <summary>
/// Regression coverage for review finding #2: <see cref="AdminMembershipMapper"/> and
/// <see cref="AdminUserCatalogAccessMapper"/> must surface per-member/per-catalog
/// <c>DiffDetails</c> instead of only reporting the whole resource as "Update", since an
/// aggregate "Update" hides exactly who gained or lost access to what.
/// </summary>
public class AdminGranularDiffMapperTests
{
    private static readonly AdminMembershipMapper MembershipMapper = new();
    private static readonly AdminUserCatalogAccessMapper CatalogAccessMapper = new();

    // ── AdminMembershipMapper ────────────────────────────────────────────────

    [Fact]
    public void Membership_MemberAdded_ReportedAsAddedDetail()
    {
        var current = new WorkspaceMembershipModel
        {
            Workspace = "analytics",
            Members = [new WorkspaceMemberEntry { Email = "a@x.com", Roles = ["WorkspaceReader"] }],
        };
        var desired = new WorkspaceMembershipModel
        {
            Workspace = "analytics",
            Members =
            [
                new WorkspaceMemberEntry { Email = "a@x.com", Roles = ["WorkspaceReader"] },
                new WorkspaceMemberEntry { Email = "b@x.com", Roles = ["WorkspaceReader"] },
            ],
        };

        var details = MembershipMapper.DiffDetails(desired, current);

        var detail = Assert.Single(details!, d => d.Field == "b@x.com");
        Assert.Equal("(none)", detail.Before);
        Assert.Equal("WorkspaceReader", detail.After);
        Assert.DoesNotContain(details!, d => d.Field == "a@x.com");
    }

    [Fact]
    public void Membership_MemberRemoved_ReportedAsRemovedDetail()
    {
        var current = new WorkspaceMembershipModel
        {
            Workspace = "analytics",
            Members =
            [
                new WorkspaceMemberEntry { Email = "a@x.com", Roles = ["WorkspaceReader"] },
                new WorkspaceMemberEntry { Email = "b@x.com", Roles = ["WorkspaceAdmin"] },
            ],
        };
        var desired = new WorkspaceMembershipModel
        {
            Workspace = "analytics",
            Members = [new WorkspaceMemberEntry { Email = "a@x.com", Roles = ["WorkspaceReader"] }],
        };

        var details = MembershipMapper.DiffDetails(desired, current);

        var detail = Assert.Single(details!, d => d.Field == "b@x.com");
        Assert.Equal("WorkspaceAdmin", detail.Before);
        Assert.Equal("(none)", detail.After);
    }

    [Fact]
    public void Membership_RoleChangedForExistingMember_ReportedWithBeforeAfterRoles()
    {
        var current = new WorkspaceMembershipModel
        {
            Workspace = "analytics",
            Members = [new WorkspaceMemberEntry { Email = "a@x.com", Roles = ["WorkspaceReader"] }],
        };
        var desired = new WorkspaceMembershipModel
        {
            Workspace = "analytics",
            Members = [new WorkspaceMemberEntry { Email = "a@x.com", Roles = ["WorkspaceAdmin"] }],
        };

        var details = MembershipMapper.DiffDetails(desired, current);

        var detail = Assert.Single(details!);
        Assert.Equal("a@x.com", detail.Field);
        Assert.Equal("WorkspaceReader", detail.Before);
        Assert.Equal("WorkspaceAdmin", detail.After);
    }

    [Fact]
    public void Membership_UnchangedMembersMixedWithChangedOnes_OnlyChangedMembersReported()
    {
        var current = new WorkspaceMembershipModel
        {
            Workspace = "analytics",
            Members =
            [
                new WorkspaceMemberEntry { Email = "a@x.com", Roles = ["WorkspaceReader"] },
                new WorkspaceMemberEntry { Email = "b@x.com", Roles = ["WorkspaceReader"] },
            ],
        };
        var desired = new WorkspaceMembershipModel
        {
            Workspace = "analytics",
            Members =
            [
                new WorkspaceMemberEntry { Email = "a@x.com", Roles = ["WorkspaceReader"] }, // unchanged
                new WorkspaceMemberEntry { Email = "b@x.com", Roles = ["WorkspaceAdmin"] },   // role changed
            ],
        };

        var details = MembershipMapper.DiffDetails(desired, current);

        var detail = Assert.Single(details!);
        Assert.Equal("b@x.com", detail.Field);
    }

    // ── AdminUserCatalogAccessMapper ─────────────────────────────────────────

    [Fact]
    public void CatalogAccess_CatalogAdded_ReportedAsGranted()
    {
        var current = new UserCatalogAccessModel { Email = "a@x.com", HasAllCatalogAccess = false, AllowedCatalogs = ["sales"] };
        var desired = new UserCatalogAccessModel { Email = "a@x.com", HasAllCatalogAccess = false, AllowedCatalogs = ["sales", "marketing"] };

        var details = CatalogAccessMapper.DiffDetails(desired, current);

        var detail = Assert.Single(details!, d => d.Field == "marketing");
        Assert.Equal("(none)", detail.Before);
        Assert.Equal("granted", detail.After);
    }

    [Fact]
    public void CatalogAccess_CatalogRemoved_ReportedAsRevoked()
    {
        var current = new UserCatalogAccessModel { Email = "a@x.com", HasAllCatalogAccess = false, AllowedCatalogs = ["sales", "marketing"] };
        var desired = new UserCatalogAccessModel { Email = "a@x.com", HasAllCatalogAccess = false, AllowedCatalogs = ["sales"] };

        var details = CatalogAccessMapper.DiffDetails(desired, current);

        var detail = Assert.Single(details!, d => d.Field == "marketing");
        Assert.Equal("granted", detail.Before);
        Assert.Equal("(none)", detail.After);
    }

    [Fact]
    public void CatalogAccess_HasAllCatalogAccessToggled_ReportedAsSingleDetail_IgnoringCatalogList()
    {
        var current = new UserCatalogAccessModel { Email = "a@x.com", HasAllCatalogAccess = false, AllowedCatalogs = ["sales"] };
        var desired = new UserCatalogAccessModel { Email = "a@x.com", HasAllCatalogAccess = true, AllowedCatalogs = [] };

        var details = CatalogAccessMapper.DiffDetails(desired, current);

        var detail = Assert.Single(details!);
        Assert.Equal("has_all_catalog_access", detail.Field);
        Assert.Equal("False", detail.Before);
        Assert.Equal("True", detail.After);
    }
}
