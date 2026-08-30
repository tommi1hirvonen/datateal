using Datateal.Core.Catalogs;
using Datateal.Core.Users;
using Datateal.Core.Workspaces;
using Datateal.Data;
using Datateal.Deployment.Models;
using Datateal.Deployment.Serialization;
using Datateal.Ui.Server.Core.Catalogs;
using Datateal.Ui.Server.Infrastructure.Deployment;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Datateal.Core.Tests.Deployment;

/// <summary>
/// Regression coverage for the admin deployment fixes (review findings b/c): stale
/// <see cref="CatalogWorkspaceAccess"/>/<see cref="WorkspaceMembership"/>/<see cref="UserCatalogAccess"/>
/// rows are now pruned when an entity IS present in the bundle (but never for entities omitted
/// entirely — "partial reconciliation"), and a failed apply after a new catalog database was
/// created logs a warning instead of attempting an active <c>DropDatabaseAsync</c> rollback (no
/// DROP DATABASE privilege required for the deployment automation path).
/// </summary>
public class AdminDeploymentServicePruningTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DatatealDbContext _db;
    private readonly FakeCatalogDatabaseService _catalogDatabaseService = new();
    private readonly RecordingLogger<AdminDeploymentService> _logger = new();

    public AdminDeploymentServicePruningTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<DatatealDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new DatatealDbContext(options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private AdminDeploymentService CreateService() => new(
        _db,
        _catalogDatabaseService,
        Options.Create(new CatalogSettings { CatalogHost = "localhost", CatalogUser = "u", CatalogPassword = "p" }),
        new EphemeralDataProtectionProvider(),
        _logger);

    private static Bundle AdminBundle(
        List<CatalogModel>? catalogs = null,
        List<WorkspaceMembershipModel>? memberships = null,
        List<UserCatalogAccessModel>? userCatalogAccess = null) => new()
        {
            Manifest = new BundleManifest { Scope = "admin" },
            Catalogs = catalogs ?? [],
            Memberships = memberships ?? [],
            UserCatalogAccess = userCatalogAccess ?? [],
        };

    // ── CatalogWorkspaceAccess pruning ──────────────────────────────────────

    [Fact]
    public async Task PruneCatalogWorkspaceAccess_RemovesGrant_NotInBundle()
    {
        var wsA = new Datateal.Core.Workspaces.Workspace { Id = Guid.NewGuid(), Name = "ws-a", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var wsB = new Datateal.Core.Workspaces.Workspace { Id = Guid.NewGuid(), Name = "ws-b", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var catalog = new ManagedCatalog { Id = Guid.NewGuid(), Name = "sales", AccessibleFromAllWorkspaces = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        _db.Workspaces.AddRange(wsA, wsB);
        _db.Catalogs.Add(catalog);
        _db.CatalogWorkspaceAccess.AddRange(
            new CatalogWorkspaceAccess { Id = Guid.NewGuid(), CatalogId = catalog.Id, WorkspaceId = wsA.Id },
            new CatalogWorkspaceAccess { Id = Guid.NewGuid(), CatalogId = catalog.Id, WorkspaceId = wsB.Id });
        await _db.SaveChangesAsync();

        var bundle = AdminBundle(catalogs:
        [
            new CatalogModel { Name = "sales", Type = "managed", DataPath = "/data/sales", AccessibleFromAllWorkspaces = false, WorkspaceAccess = ["ws-a"] },
        ]);

        await CreateService().ApplyAsync(bundle);

        var remaining = await _db.CatalogWorkspaceAccess.Where(a => a.CatalogId == catalog.Id).ToListAsync();
        var grant = Assert.Single(remaining);
        Assert.Equal(wsA.Id, grant.WorkspaceId);
    }

    [Fact]
    public async Task AccessibleFromAllWorkspaces_True_ClearsAllStaleGrants()
    {
        var wsA = new Datateal.Core.Workspaces.Workspace { Id = Guid.NewGuid(), Name = "ws-a", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var catalog = new ManagedCatalog { Id = Guid.NewGuid(), Name = "sales", AccessibleFromAllWorkspaces = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        _db.Workspaces.Add(wsA);
        _db.Catalogs.Add(catalog);
        _db.CatalogWorkspaceAccess.Add(new CatalogWorkspaceAccess { Id = Guid.NewGuid(), CatalogId = catalog.Id, WorkspaceId = wsA.Id });
        await _db.SaveChangesAsync();

        var bundle = AdminBundle(catalogs:
        [
            new CatalogModel { Name = "sales", Type = "managed", DataPath = "/data/sales", AccessibleFromAllWorkspaces = true },
        ]);

        await CreateService().ApplyAsync(bundle);

        Assert.Empty(await _db.CatalogWorkspaceAccess.Where(a => a.CatalogId == catalog.Id).ToListAsync());
    }

    // ── WorkspaceMembership pruning ─────────────────────────────────────────

    [Fact]
    public async Task PruneWorkspaceMembership_RemovesMember_NotInBundle()
    {
        var ws = new Datateal.Core.Workspaces.Workspace { Id = Guid.NewGuid(), Name = "analytics", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var userA = new UserAccount { Id = Guid.NewGuid(), Email = "a@x.com", DisplayName = "A", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var userB = new UserAccount { Id = Guid.NewGuid(), Email = "b@x.com", DisplayName = "B", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        _db.Workspaces.Add(ws);
        _db.AppUsers.AddRange(userA, userB);
        _db.WorkspaceMemberships.AddRange(
            new WorkspaceMembership { Id = Guid.NewGuid(), WorkspaceId = ws.Id, UserId = userA.Id, Roles = ["WorkspaceReader"], CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow },
            new WorkspaceMembership { Id = Guid.NewGuid(), WorkspaceId = ws.Id, UserId = userB.Id, Roles = ["WorkspaceReader"], CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        var bundle = AdminBundle(memberships:
        [
            new WorkspaceMembershipModel
            {
                Workspace = "analytics",
                Members = [new WorkspaceMemberEntry { Email = "a@x.com", Roles = ["WorkspaceReader"] }],
            },
        ]);

        await CreateService().ApplyAsync(bundle);

        var remaining = await _db.WorkspaceMemberships.Where(m => m.WorkspaceId == ws.Id).ToListAsync();
        var membership = Assert.Single(remaining);
        Assert.Equal(userA.Id, membership.UserId);
    }

    [Fact]
    public async Task PartialReconciliation_WorkspaceNotInBundle_MembershipsUntouched()
    {
        var managed = new Datateal.Core.Workspaces.Workspace { Id = Guid.NewGuid(), Name = "analytics", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var unmanaged = new Datateal.Core.Workspaces.Workspace { Id = Guid.NewGuid(), Name = "finance", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var user = new UserAccount { Id = Guid.NewGuid(), Email = "a@x.com", DisplayName = "A", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        _db.Workspaces.AddRange(managed, unmanaged);
        _db.AppUsers.Add(user);
        _db.WorkspaceMemberships.Add(
            new WorkspaceMembership { Id = Guid.NewGuid(), WorkspaceId = unmanaged.Id, UserId = user.Id, Roles = ["WorkspaceReader"], CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        // Bundle only manages "analytics" (empty member list); "finance" is never mentioned.
        var bundle = AdminBundle(memberships:
        [
            new WorkspaceMembershipModel { Workspace = "analytics", Members = [] },
        ]);

        await CreateService().ApplyAsync(bundle);

        Assert.Single(await _db.WorkspaceMemberships.Where(m => m.WorkspaceId == unmanaged.Id).ToListAsync());
    }

    // ── UserCatalogAccess pruning ────────────────────────────────────────────

    [Fact]
    public async Task PruneUserCatalogAccess_RemovesCatalog_NotInBundle()
    {
        var catalogSales = new ManagedCatalog { Id = Guid.NewGuid(), Name = "sales", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var catalogMarketing = new ManagedCatalog { Id = Guid.NewGuid(), Name = "marketing", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var user = new UserAccount { Id = Guid.NewGuid(), Email = "a@x.com", DisplayName = "A", HasAllCatalogAccess = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        _db.Catalogs.AddRange(catalogSales, catalogMarketing);
        _db.AppUsers.Add(user);
        _db.UserCatalogAccess.AddRange(
            new UserCatalogAccess { Id = Guid.NewGuid(), UserId = user.Id, CatalogId = catalogSales.Id },
            new UserCatalogAccess { Id = Guid.NewGuid(), UserId = user.Id, CatalogId = catalogMarketing.Id });
        await _db.SaveChangesAsync();

        var bundle = AdminBundle(
            catalogs:
            [
                new CatalogModel { Name = "sales", Type = "managed", DataPath = "/data/sales" },
                new CatalogModel { Name = "marketing", Type = "managed", DataPath = "/data/marketing" },
            ],
            userCatalogAccess:
            [
                new UserCatalogAccessModel { Email = "a@x.com", HasAllCatalogAccess = false, AllowedCatalogs = ["sales"] },
            ]);

        await CreateService().ApplyAsync(bundle);

        var remaining = await _db.UserCatalogAccess.Where(a => a.UserId == user.Id).ToListAsync();
        var access = Assert.Single(remaining);
        Assert.Equal(catalogSales.Id, access.CatalogId);
    }

    [Fact]
    public async Task HasAllCatalogAccess_True_ClearsAllStaleGrants()
    {
        var catalog = new ManagedCatalog { Id = Guid.NewGuid(), Name = "sales", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var user = new UserAccount { Id = Guid.NewGuid(), Email = "a@x.com", DisplayName = "A", HasAllCatalogAccess = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        _db.Catalogs.Add(catalog);
        _db.AppUsers.Add(user);
        _db.UserCatalogAccess.Add(new UserCatalogAccess { Id = Guid.NewGuid(), UserId = user.Id, CatalogId = catalog.Id });
        await _db.SaveChangesAsync();

        var bundle = AdminBundle(
            catalogs: [new CatalogModel { Name = "sales", Type = "managed", DataPath = "/data/sales" }],
            userCatalogAccess: [new UserCatalogAccessModel { Email = "a@x.com", HasAllCatalogAccess = true }]);

        await CreateService().ApplyAsync(bundle);

        Assert.Empty(await _db.UserCatalogAccess.Where(a => a.UserId == user.Id).ToListAsync());
    }

    [Fact]
    public async Task PartialReconciliation_UserNotInBundle_AccessUntouched()
    {
        var catalog = new ManagedCatalog { Id = Guid.NewGuid(), Name = "sales", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var mentionedUser = new UserAccount { Id = Guid.NewGuid(), Email = "a@x.com", DisplayName = "A", HasAllCatalogAccess = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var unmentionedUser = new UserAccount { Id = Guid.NewGuid(), Email = "b@x.com", DisplayName = "B", HasAllCatalogAccess = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        _db.Catalogs.Add(catalog);
        _db.AppUsers.AddRange(mentionedUser, unmentionedUser);
        _db.UserCatalogAccess.Add(new UserCatalogAccess { Id = Guid.NewGuid(), UserId = unmentionedUser.Id, CatalogId = catalog.Id });
        await _db.SaveChangesAsync();

        var bundle = AdminBundle(
            catalogs: [new CatalogModel { Name = "sales", Type = "managed", DataPath = "/data/sales" }],
            userCatalogAccess: [new UserCatalogAccessModel { Email = "a@x.com", HasAllCatalogAccess = false, AllowedCatalogs = ["sales"] }]);

        await CreateService().ApplyAsync(bundle);

        Assert.Single(await _db.UserCatalogAccess.Where(a => a.UserId == unmentionedUser.Id).ToListAsync());
    }

    // ── No active DROP-based rollback; warn instead ─────────────────────────

    [Fact]
    public async Task FailureAfterCatalogDbCreated_LogsWarning_DoesNotDropDatabase()
    {
        // Catalogs are processed in alphabetical order (see NormalizeCatalogs); "aaa-first" must
        // succeed before "zzz-second" fails, so its database creation is the one already recorded
        // when the transaction attempt fails.
        _catalogDatabaseService.CreatedResultsByName["aaa-first"] = true;
        _catalogDatabaseService.ThrowOnCreateForName = "zzz-second";

        var bundle = AdminBundle(catalogs:
        [
            new CatalogModel { Name = "aaa-first", Type = "managed", DataPath = "/data/aaa-first" },
            new CatalogModel { Name = "zzz-second", Type = "managed", DataPath = "/data/zzz-second" },
        ]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService().ApplyAsync(bundle));

        Assert.Contains("aaa-first", _catalogDatabaseService.CreateCalls);
        Assert.Contains("zzz-second", _catalogDatabaseService.CreateCalls);
        Assert.Empty(_catalogDatabaseService.DropCalls);
        Assert.Contains(_logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("aaa-first"));

        // The catalog row itself must not have been persisted (transaction rolled back).
        Assert.False(await _db.Catalogs.AnyAsync(c => c.Name == "aaa-first"));
    }

    // ── Test doubles ─────────────────────────────────────────────────────────

    private sealed class FakeCatalogDatabaseService : ICatalogDatabaseService
    {
        public List<string> CreateCalls { get; } = [];
        public List<string> DropCalls { get; } = [];
        public Dictionary<string, bool> CreatedResultsByName { get; } = [];
        public string? ThrowOnCreateForName { get; set; }

        public Task<bool> CreateDatabaseAsync(string databaseName, string host, int port, string user, string password,
            bool allowExistingDatabase = false, CancellationToken cancellationToken = default)
        {
            CreateCalls.Add(databaseName);
            if (string.Equals(databaseName, ThrowOnCreateForName, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Simulated failure creating database '{databaseName}'.");

            return Task.FromResult(CreatedResultsByName.TryGetValue(databaseName, out var created) ? created : true);
        }

        public Task DropDatabaseAsync(string databaseName, string host, int port, string user, string password,
            CancellationToken cancellationToken = default)
        {
            DropCalls.Add(databaseName);
            return Task.CompletedTask;
        }

        public Task<(bool ParquetV2, bool PerThreadOutput)?> GetDuckLakeSettingsAsync(
            string host, int port, string database, string user, string password,
            CancellationToken cancellationToken = default) => Task.FromResult<(bool, bool)?>(null);

        public Task SetDuckLakeSettingsAsync(
            string host, int port, string database, string user, string password,
            string dataPath, string? storageConnectionString, string catalogName,
            bool parquetV2, bool perThreadOutput,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}
