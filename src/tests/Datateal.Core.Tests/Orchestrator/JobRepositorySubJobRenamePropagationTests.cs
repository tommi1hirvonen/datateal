using Datateal.Data;
using Datateal.Orchestrator.Core.Entities;
using Datateal.Orchestrator.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Datateal.Core.Tests.Orchestrator;

/// <summary>
/// Regression coverage for propagating a job rename into any <c>SubJobTask.SubJobName</c>
/// reference pointing at it (see <c>JobRepository.UpdateJobAsync</c>). Sub-job tasks reference a
/// job by name rather than a persisted id, so renaming a job in place must repoint any stale
/// references itself — mirrors the notebook/query path-repoint fix in
/// <c>WorkspaceRepository</c>.
/// </summary>
public class JobRepositorySubJobRenamePropagationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DatatealDbContext _db;
    private readonly JobRepository _repository;
    private readonly Guid _workspaceId = Guid.NewGuid();

    public JobRepositorySubJobRenamePropagationTests()
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

    private Job AddJob(string name)
    {
        var job = new Job { Id = Guid.NewGuid(), WorkspaceId = _workspaceId, Name = name };
        _db.Jobs.Add(job);
        _db.SaveChanges();
        return job;
    }

    private Job AddJobWithSubJobTask(string name, string subJobName)
    {
        var job = new Job { Id = Guid.NewGuid(), WorkspaceId = _workspaceId, Name = name };
        job.Tasks.Add(new SubJobTask { Id = Guid.NewGuid(), JobId = job.Id, Name = "task", SubJobName = subJobName });
        _db.Jobs.Add(job);
        _db.SaveChanges();
        return job;
    }

    private string? GetStoredSubJobName(Guid jobId) =>
        _db.JobTasks.OfType<SubJobTask>().AsNoTracking().Single(t => t.JobId == jobId).SubJobName;

    [Fact]
    public async Task UpdateJobAsync_Rename_RepointsOtherJobsSubJobTaskReferences()
    {
        var childJob = AddJob("Old Child Name");
        var parentJob = AddJobWithSubJobTask("Parent Job", "Old Child Name");

        childJob.Name = "New Child Name";
        await _repository.UpdateJobAsync(childJob);

        Assert.Equal("New Child Name", GetStoredSubJobName(parentJob.Id));
    }

    [Fact]
    public async Task UpdateJobAsync_NoNameChange_LeavesSubJobTaskReferencesUntouched()
    {
        var childJob = AddJob("Child Name");
        var parentJob = AddJobWithSubJobTask("Parent Job", "Child Name");

        childJob.Description = "just updating an unrelated field";
        await _repository.UpdateJobAsync(childJob);

        Assert.Equal("Child Name", GetStoredSubJobName(parentJob.Id));
    }

    [Fact]
    public async Task UpdateJobAsync_Rename_DoesNotAffectSubJobTasksInOtherWorkspaces()
    {
        var otherWorkspaceId = Guid.NewGuid();
        _db.Workspaces.Add(new Datateal.Core.Workspaces.Workspace { Id = otherWorkspaceId, Name = "Other Workspace" });
        await _db.SaveChangesAsync();

        var childJob = AddJob("Shared Name");

        var otherParentJob = new Job { Id = Guid.NewGuid(), WorkspaceId = otherWorkspaceId, Name = "Other Parent" };
        otherParentJob.Tasks.Add(new SubJobTask { Id = Guid.NewGuid(), JobId = otherParentJob.Id, Name = "task", SubJobName = "Shared Name" });
        _db.Jobs.Add(otherParentJob);
        await _db.SaveChangesAsync();

        childJob.Name = "Renamed";
        await _repository.UpdateJobAsync(childJob);

        // The other workspace's identically-named reference must be untouched — a job in a
        // different workspace happening to share the same name is unrelated.
        Assert.Equal("Shared Name", GetStoredSubJobName(otherParentJob.Id));
    }
}
