using Datateal.Auth;
using Datateal.Data.Catalogs;

namespace Datateal.Core.Tests.Catalogs;

/// <summary>
/// Tests the pure catalog-access decision logic shared by the interactive UI path and the job
/// execution path. This is the security boundary that authorizes which catalogs a job's effective
/// owner may attach at run time.
/// </summary>
public class CatalogAccessRulesTests
{
    private static CatalogAccessRules.UserCatalogProfile Profile(
        bool hasAll = false, IEnumerable<string>? roles = null, IEnumerable<Guid>? grants = null) =>
        new(roles?.ToList() ?? [], hasAll, grants?.ToList() ?? []);

    // ── User tier ────────────────────────────────────────────────────────

    [Fact]
    public void UserTier_NullUserId_IsUnrestricted()
    {
        Assert.Null(CatalogAccessRules.ResolveUserTier(null, null));
    }

    [Fact]
    public void UserTier_UserIdButNoProfile_FailsClosed()
    {
        var result = CatalogAccessRules.ResolveUserTier(Guid.NewGuid(), null);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void UserTier_AdminRole_IsUnrestricted()
    {
        var result = CatalogAccessRules.ResolveUserTier(
            Guid.NewGuid(), Profile(roles: [DatatealRole.Admin], grants: [Guid.NewGuid()]));

        Assert.Null(result);
    }

    [Fact]
    public void UserTier_CatalogContributorRole_IsUnrestricted()
    {
        var result = CatalogAccessRules.ResolveUserTier(
            Guid.NewGuid(), Profile(roles: [DatatealRole.CatalogContributor]));

        Assert.Null(result);
    }

    [Fact]
    public void UserTier_HasAllCatalogAccessFlag_IsUnrestricted()
    {
        var result = CatalogAccessRules.ResolveUserTier(Guid.NewGuid(), Profile(hasAll: true));

        Assert.Null(result);
    }

    [Fact]
    public void UserTier_ExplicitGrants_ReturnsExactGrants()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        var result = CatalogAccessRules.ResolveUserTier(Guid.NewGuid(), Profile(grants: [a, b]));

        Assert.NotNull(result);
        Assert.Equal(new HashSet<Guid> { a, b }, result);
    }

    [Fact]
    public void UserTier_NoGrantsNoRolesNoFlag_ReturnsEmpty()
    {
        var result = CatalogAccessRules.ResolveUserTier(Guid.NewGuid(), Profile());

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public void UserTier_PerWorkspaceRoleInTenantStore_DoesNotGrantUnrestricted()
    {
        // A non-catalog role must not be treated as unrestricted catalog access.
        var grant = Guid.NewGuid();
        var result = CatalogAccessRules.ResolveUserTier(
            Guid.NewGuid(), Profile(roles: [DatatealRole.JobOperator], grants: [grant]));

        Assert.NotNull(result);
        Assert.Equal(new HashSet<Guid> { grant }, result);
    }

    // ── Workspace tier ───────────────────────────────────────────────────

    [Fact]
    public void WorkspaceTier_NullWorkspace_IsUnrestricted()
    {
        Assert.Null(CatalogAccessRules.ResolveWorkspaceTier(null, [Guid.NewGuid()], [Guid.NewGuid()]));
    }

    [Fact]
    public void WorkspaceTier_UnionsOpenAndGranted()
    {
        var open = Guid.NewGuid();
        var granted = Guid.NewGuid();

        var result = CatalogAccessRules.ResolveWorkspaceTier(Guid.NewGuid(), [open], [granted]);

        Assert.NotNull(result);
        Assert.Equal(new HashSet<Guid> { open, granted }, result);
    }

    [Fact]
    public void WorkspaceTier_NoCatalogs_ReturnsEmpty()
    {
        var result = CatalogAccessRules.ResolveWorkspaceTier(Guid.NewGuid(), [], []);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    // ── Combine (intersection) ───────────────────────────────────────────

    [Fact]
    public void Combine_BothNull_IsNull()
    {
        Assert.Null(CatalogAccessRules.Combine(null, null));
    }

    [Fact]
    public void Combine_LeftNull_ReturnsRight()
    {
        var b = new HashSet<Guid> { Guid.NewGuid() };
        Assert.Same(b, CatalogAccessRules.Combine(null, b));
    }

    [Fact]
    public void Combine_RightNull_ReturnsLeft()
    {
        var a = new HashSet<Guid> { Guid.NewGuid() };
        Assert.Same(a, CatalogAccessRules.Combine(a, null));
    }

    [Fact]
    public void Combine_TwoSets_ReturnsIntersection()
    {
        var shared = Guid.NewGuid();
        var a = new HashSet<Guid> { shared, Guid.NewGuid() };
        var b = new HashSet<Guid> { shared, Guid.NewGuid() };

        var result = CatalogAccessRules.Combine(a, b);

        Assert.NotNull(result);
        Assert.Equal(new HashSet<Guid> { shared }, result);
    }

    [Fact]
    public void Combine_DisjointSets_ReturnsEmpty()
    {
        var a = new HashSet<Guid> { Guid.NewGuid() };
        var b = new HashSet<Guid> { Guid.NewGuid() };

        var result = CatalogAccessRules.Combine(a, b);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    // ── End-to-end composition: the effective access decision ────────────

    [Fact]
    public void Effective_RestrictedUser_BoundedByUserGrantsWithinWorkspace()
    {
        // User is granted catalog A only; workspace exposes A and B.
        // Effective access must be A only — the confused-deputy fix: user grants bound the job.
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        var userTier = CatalogAccessRules.ResolveUserTier(Guid.NewGuid(), Profile(grants: [a]));
        var workspaceTier = CatalogAccessRules.ResolveWorkspaceTier(Guid.NewGuid(), [a, b], []);
        var effective = CatalogAccessRules.Combine(userTier, workspaceTier);

        Assert.NotNull(effective);
        Assert.Equal(new HashSet<Guid> { a }, effective);
    }

    [Fact]
    public void Effective_AdminUser_BoundedOnlyByWorkspace()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        var userTier = CatalogAccessRules.ResolveUserTier(Guid.NewGuid(), Profile(roles: [DatatealRole.Admin]));
        var workspaceTier = CatalogAccessRules.ResolveWorkspaceTier(Guid.NewGuid(), [a], [b]);
        var effective = CatalogAccessRules.Combine(userTier, workspaceTier);

        Assert.NotNull(effective);
        Assert.Equal(new HashSet<Guid> { a, b }, effective);
    }

    [Fact]
    public void Effective_DeletedOwner_HasNoAccess()
    {
        // Owner id supplied but no profile (deleted user) → empty user tier → empty effective set.
        var userTier = CatalogAccessRules.ResolveUserTier(Guid.NewGuid(), null);
        var workspaceTier = CatalogAccessRules.ResolveWorkspaceTier(Guid.NewGuid(), [Guid.NewGuid()], []);
        var effective = CatalogAccessRules.Combine(userTier, workspaceTier);

        Assert.NotNull(effective);
        Assert.Empty(effective);
    }
}
