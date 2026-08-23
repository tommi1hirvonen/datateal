using Datateal.Core.Catalogs;
using Datateal.Core.Deployment;
using Datateal.Core.Users;
using Datateal.Data;
using Datateal.Deployment.Models;
using Datateal.Deployment.Serialization;
using Datateal.Ui.Server.Application.Mediator.Commands;
using Datateal.Ui.Server.Core.Catalogs;
using Datateal.Ui.Server.Core.Repositories;
using Datateal.Ui.Server.Infrastructure.Deployment;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Datateal.Core.Tests.Deployment;

/// <summary>
/// Regression coverage for review finding #1: admin-scope deployments must be audited (a
/// <see cref="DeploymentLog"/> with <see cref="DeploymentScope.Admin"/> recording who applied
/// what, and its outcome), and a stray in-progress admin log left behind by a crash must be swept
/// to <see cref="DeploymentStatus.Failed"/> on startup. Unlike workspace deployments, this is
/// audit-only — an admin apply is a single atomic transaction, so a crash mid-apply is already
/// safely rolled back by the database itself; there is no snapshot restoration to perform.
/// </summary>
public class AdminDeploymentAuditLogTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DatatealDbContext _db;
    private readonly FakeCatalogDatabaseService _catalogDatabaseService = new();
    private readonly RecordingLogger<AdminDeploymentService> _logger = new();
    private readonly FakeUserRepository _userRepository = new();
    private readonly DeploymentLockManager _lockManager = new();

    public AdminDeploymentAuditLogTests()
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

    private AdminDeploymentService CreateAdminService() => new(
        _db,
        _catalogDatabaseService,
        Options.Create(new CatalogSettings { CatalogHost = "localhost", CatalogUser = "u", CatalogPassword = "p" }),
        new EphemeralDataProtectionProvider(),
        _logger);

    private ApplyAdminDeploymentHandler CreateHandler() => new(CreateAdminService(), _userRepository, _lockManager);

    [Fact]
    public async Task Apply_Success_CreatesCompletedAuditLog_WithRedactedPasswordAndResolvedActorName()
    {
        var actingUserId = Guid.NewGuid();
        _userRepository.Users[actingUserId] = new UserAccount
        {
            Id = actingUserId,
            Email = "admin@x.com",
            DisplayName = "Admin User",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        var bundle = new Bundle
        {
            Manifest = new BundleManifest { Scope = "admin" },
            Catalogs =
            [
                new CatalogModel
                {
                    Name = "finance",
                    Type = "unmanaged",
                    DataPath = "/data/finance",
                    CatalogHost = "db.internal",
                    CatalogDatabase = "finance",
                    CatalogUser = "svc",
                    CatalogPassword = "super-secret",
                },
            ],
        };

        await CreateHandler().Handle(new ApplyAdminDeploymentRequest(bundle, actingUserId.ToString()), CancellationToken.None);

        var log = Assert.Single(await _db.DeploymentLogs.ToListAsync());
        Assert.Equal(DeploymentScope.Admin, log.Scope);
        Assert.Null(log.WorkspaceId);
        Assert.Equal(DeploymentStatus.Completed, log.Status);
        Assert.Equal(actingUserId.ToString(), log.IssuedByUserId);
        Assert.Equal("Admin User (admin@x.com)", log.IssuedByDisplayName);
        Assert.NotNull(log.CompletedAt);

        // The literal catalog password must never be persisted in the audit log (System.Text.Json
        // escapes '<'/'>' as \u003C/\u003E by default, so check for the unescaped substring).
        Assert.DoesNotContain("super-secret", log.TargetBundleJson);
        Assert.Contains("redacted", log.TargetBundleJson);
    }

    [Fact]
    public async Task Apply_Failure_CreatesFailedAuditLogWithReason_AndTransactionRollsBack()
    {
        _catalogDatabaseService.ThrowOnCreateForName = "boom";
        var bundle = new Bundle
        {
            Manifest = new BundleManifest { Scope = "admin" },
            Catalogs = [new CatalogModel { Name = "boom", Type = "managed", DataPath = "/data/boom" }],
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateHandler().Handle(new ApplyAdminDeploymentRequest(bundle, ActingUserId: null), CancellationToken.None));

        var log = Assert.Single(await _db.DeploymentLogs.ToListAsync());
        Assert.Equal(DeploymentStatus.Failed, log.Status);
        Assert.False(string.IsNullOrWhiteSpace(log.FailureReason));

        // The transaction must have rolled back — the catalog row must not exist.
        Assert.False(await _db.Catalogs.AnyAsync(c => c.Name == "boom"));
    }

    [Fact]
    public async Task RecoverAdminLogAsync_StrayStagingLog_IsSweptToFailed()
    {
        var log = DeploymentLog.CreateForAdmin("{}", "{}");
        _db.DeploymentLogs.Add(log);
        await _db.SaveChangesAsync();

        await DeploymentRecoveryBackgroundService.RecoverAdminLogAsync(
            _db, _lockManager, NullLogger<DeploymentRecoveryBackgroundService>.Instance, log, CancellationToken.None);

        var reloaded = await _db.DeploymentLogs.SingleAsync(l => l.Id == log.Id);
        Assert.Equal(DeploymentStatus.Failed, reloaded.Status);
        Assert.False(string.IsNullOrWhiteSpace(reloaded.FailureReason));
        Assert.NotNull(reloaded.CompletedAt);
    }

    [Fact]
    public async Task RecoverAdminLogAsync_StrayApplyingUiLog_IsSweptToFailed()
    {
        var log = DeploymentLog.CreateForAdmin("{}", "{}");
        log.TransitionToApplyingUi();
        _db.DeploymentLogs.Add(log);
        await _db.SaveChangesAsync();

        await DeploymentRecoveryBackgroundService.RecoverAdminLogAsync(
            _db, _lockManager, NullLogger<DeploymentRecoveryBackgroundService>.Instance, log, CancellationToken.None);

        var reloaded = await _db.DeploymentLogs.SingleAsync(l => l.Id == log.Id);
        Assert.Equal(DeploymentStatus.Failed, reloaded.Status);
    }

    // ── Test doubles ─────────────────────────────────────────────────────────

    private sealed class FakeUserRepository : IUserRepository
    {
        public Dictionary<Guid, AppUser> Users { get; } = [];

        public Task<AppUser?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Users.TryGetValue(id, out var user) ? user : null);

        public Task<IReadOnlyList<UserAccount>> GetAllUserAccountsAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<ServiceAccount>> GetAllServiceAccountsAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<AppUser?> GetByEmailAsync(string email, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> EmailExistsAsync(string email, Guid? excludeId = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<AppUser>> GetByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<AppUser> CreateAsync(AppUser user, CancellationToken ct = default) => throw new NotImplementedException();

        public Task<AppUser?> UpdateAsync(Guid id, string displayName, bool isEnabled, List<string> roles, bool hasAllCatalogAccess,
            List<Guid> catalogIds, CancellationToken ct = default) => throw new NotImplementedException();

        public Task<ServiceAccount?> UpdateServiceAccountAsync(Guid id, string name, string? description, bool isEnabled,
            List<string> roles, bool hasAllCatalogAccess, List<Guid> catalogIds, CancellationToken ct = default) => throw new NotImplementedException();

        public Task<int> GetActiveTokenCountByActingUserAsync(Guid userId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> DeleteAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class FakeCatalogDatabaseService : ICatalogDatabaseService
    {
        public string? ThrowOnCreateForName { get; set; }

        public Task<bool> CreateDatabaseAsync(string databaseName, string host, int port, string user, string password,
            bool allowExistingDatabase = false, CancellationToken cancellationToken = default)
        {
            if (string.Equals(databaseName, ThrowOnCreateForName, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Simulated failure creating database '{databaseName}'.");

            return Task.FromResult(true);
        }

        public Task DropDatabaseAsync(string databaseName, string host, int port, string user, string password,
            CancellationToken cancellationToken = default) => Task.CompletedTask;

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
