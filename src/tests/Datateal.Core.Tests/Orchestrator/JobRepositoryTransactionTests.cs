using Datateal.Data;
using Datateal.Orchestrator.Core.Entities;
using Datateal.Orchestrator.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Datateal.Core.Tests.Orchestrator;

/// <summary>
/// Coverage for <see cref="JobRepository.ExecuteInTransactionAsync{T}"/>, which the orchestrator's
/// job-apply deployment loop (<c>ApplyJobDeploymentHandler</c>) uses to make the whole batch of
/// job creates/updates/deletes atomic, and for <see cref="JobRepository.UpdateJobAsync"/>'s
/// ambient-transaction detection, which must avoid starting a nested transaction/execution
/// strategy when already running inside one of these batches.
/// </summary>
public class JobRepositoryTransactionTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DatatealDbContext _db;
    private readonly JobRepository _repository;
    private readonly Guid _workspaceId = Guid.NewGuid();

    public JobRepositoryTransactionTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<DatatealDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new DatatealDbContext(options);
        _db.Database.EnsureCreated();
        _repository = new JobRepository(_db);

        _db.Workspaces.Add(new Datateal.Core.Workspaces.Workspace { Id = _workspaceId, Name = "Test Workspace" });
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_ActionSucceeds_CommitsAllChanges()
    {
        var jobId = Guid.NewGuid();

        var result = await _repository.ExecuteInTransactionAsync(async ct =>
        {
            await _repository.CreateJobAsync(new Job { Id = jobId, WorkspaceId = _workspaceId, Name = "Job A" }, ct);
            await _repository.CreateJobAsync(new Job { Id = Guid.NewGuid(), WorkspaceId = _workspaceId, Name = "Job B" }, ct);
            return 2;
        });

        Assert.Equal(2, result);
        Assert.Equal(2, await _db.Jobs.CountAsync());
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_LaterActionFails_RollsBackEarlierChangesInSameCall()
    {
        // Simulates the job-apply loop: first job creation succeeds, second fails (e.g. duplicate
        // name / validation error) — the first must not be left committed.
        await Assert.ThrowsAsync<InvalidOperationException>(() => _repository.ExecuteInTransactionAsync<object?>(async ct =>
        {
            await _repository.CreateJobAsync(new Job { Id = Guid.NewGuid(), WorkspaceId = _workspaceId, Name = "Job A" }, ct);
            throw new InvalidOperationException("simulated failure applying a later job");
        }));

        Assert.Empty(await _db.Jobs.ToListAsync());
    }

    [Fact]
    public async Task UpdateJobAsync_CalledStandalone_StartsItsOwnTransaction()
    {
        var job = new Job { Id = Guid.NewGuid(), WorkspaceId = _workspaceId, Name = "Original" };
        _db.Jobs.Add(job);
        await _db.SaveChangesAsync();

        job.Name = "Renamed";
        var updated = await _repository.UpdateJobAsync(job);

        Assert.NotNull(updated);
        Assert.Equal("Renamed", updated!.Name);
        Assert.Null(_db.Database.CurrentTransaction);
    }

    [Fact]
    public async Task UpdateJobAsync_CalledInsideAmbientTransaction_DoesNotStartNestedTransaction()
    {
        var job = new Job { Id = Guid.NewGuid(), WorkspaceId = _workspaceId, Name = "Original" };
        _db.Jobs.Add(job);
        await _db.SaveChangesAsync();

        var result = await _repository.ExecuteInTransactionAsync(async ct =>
        {
            // If UpdateJobAsync tried to start its own BeginTransactionAsync here, Sqlite would
            // throw ("A transaction is already in progress"), failing this call.
            job.Name = "Renamed";
            return await _repository.UpdateJobAsync(job, ct);
        });

        Assert.NotNull(result);
        Assert.Equal("Renamed", result!.Name);
    }

    [Fact]
    public async Task UpdateJobAsync_InsideAmbientTransaction_RollsBackWithOuterTransactionOnFailure()
    {
        var job = new Job { Id = Guid.NewGuid(), WorkspaceId = _workspaceId, Name = "Original" };
        _db.Jobs.Add(job);
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => _repository.ExecuteInTransactionAsync<object?>(async ct =>
        {
            job.Name = "Renamed";
            await _repository.UpdateJobAsync(job, ct);
            throw new InvalidOperationException("simulated failure of a later step in the batch");
        }));

        var reloaded = await _db.Jobs.AsNoTracking().SingleAsync(j => j.Id == job.Id);
        Assert.Equal("Original", reloaded.Name);
    }
}
