using Datateal.Deployment.Models;
using Datateal.Ui.Server.Infrastructure.Deployment;

namespace Datateal.Core.Tests.Deployment;

/// <summary>
/// Regression coverage for the "additive-only" bug (review finding c): the admin bundle mappers'
/// <c>AreEqual</c> used to perform a one-directional subset check (every desired item exists in
/// current) instead of full set equality. That meant removing an item from a bundle's access list
/// was reported as <see cref="Datateal.Deployment.Diff.ChangeType.NoChange"/> instead of
/// <see cref="Datateal.Deployment.Diff.ChangeType.Update"/>, so the (already additive-only) apply
/// logic never even ran for the removal case. These tests assert the fixed, symmetric behavior.
/// </summary>
public class AdminAccessMapperEqualityTests
{
    [Fact]
    public void CatalogMapper_AreEqual_False_WhenWorkspaceAccessShrinks()
    {
        var mapper = new AdminCatalogMapper();
        var current = new CatalogModel
        {
            Name = "sales",
            Type = "managed",
            AccessibleFromAllWorkspaces = false,
            WorkspaceAccess = ["ws-a", "ws-b"],
        };
        var desired = new CatalogModel
        {
            Name = "sales",
            Type = "managed",
            AccessibleFromAllWorkspaces = false,
            WorkspaceAccess = ["ws-a"],
        };

        Assert.False(mapper.AreEqual(desired, current));
    }

    [Fact]
    public void CatalogMapper_AreEqual_True_WhenWorkspaceAccessUnchanged()
    {
        var mapper = new AdminCatalogMapper();
        var current = new CatalogModel
        {
            Name = "sales",
            Type = "managed",
            AccessibleFromAllWorkspaces = false,
            WorkspaceAccess = ["ws-a", "ws-b"],
        };
        var desired = new CatalogModel
        {
            Name = "sales",
            Type = "managed",
            AccessibleFromAllWorkspaces = false,
            WorkspaceAccess = ["ws-b", "ws-a"],
        };

        Assert.True(mapper.AreEqual(desired, current));
    }

    [Fact]
    public void MembershipMapper_AreEqual_False_WhenMemberRemoved()
    {
        var mapper = new AdminMembershipMapper();
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
            Members = [new WorkspaceMemberEntry { Email = "a@x.com", Roles = ["WorkspaceReader"] }],
        };

        Assert.False(mapper.AreEqual(desired, current));
    }

    [Fact]
    public void MembershipMapper_AreEqual_True_WhenMembersUnchanged()
    {
        var mapper = new AdminMembershipMapper();
        var current = new WorkspaceMembershipModel
        {
            Workspace = "analytics",
            Members = [new WorkspaceMemberEntry { Email = "a@x.com", Roles = ["WorkspaceReader"] }],
        };
        var desired = new WorkspaceMembershipModel
        {
            Workspace = "analytics",
            Members = [new WorkspaceMemberEntry { Email = "a@x.com", Roles = ["WorkspaceReader"] }],
        };

        Assert.True(mapper.AreEqual(desired, current));
    }

    [Fact]
    public void UserCatalogAccessMapper_AreEqual_False_WhenAllowedCatalogShrinks()
    {
        var mapper = new AdminUserCatalogAccessMapper();
        var current = new UserCatalogAccessModel
        {
            Email = "a@x.com",
            HasAllCatalogAccess = false,
            AllowedCatalogs = ["sales", "marketing"],
        };
        var desired = new UserCatalogAccessModel
        {
            Email = "a@x.com",
            HasAllCatalogAccess = false,
            AllowedCatalogs = ["sales"],
        };

        Assert.False(mapper.AreEqual(desired, current));
    }

    [Fact]
    public void UserCatalogAccessMapper_AreEqual_True_WhenAllowedCatalogsUnchanged()
    {
        var mapper = new AdminUserCatalogAccessMapper();
        var current = new UserCatalogAccessModel
        {
            Email = "a@x.com",
            HasAllCatalogAccess = false,
            AllowedCatalogs = ["marketing", "sales"],
        };
        var desired = new UserCatalogAccessModel
        {
            Email = "a@x.com",
            HasAllCatalogAccess = false,
            AllowedCatalogs = ["sales", "marketing"],
        };

        Assert.True(mapper.AreEqual(desired, current));
    }
}
