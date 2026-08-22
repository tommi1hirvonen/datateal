using Datateal.Core.Workspace;
using Datateal.Data;
using Datateal.Orchestrator.Core.Entities;
using Datateal.Ui.Server.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Datateal.Core.Tests.Workspace;

/// <summary>
/// Regression coverage for propagating a notebook/query rename or move into any orchestrator job
/// task that references it by path (see <c>WorkspaceRepository.RepointJobTaskPathAsync</c>). Job
/// tasks store a workspace-relative path rather than a persisted id (see Plan 2 in the session's
/// plan.md), so an interactive rename/move must repoint any stale references itself — this did not
/// happen automatically before this fix, unlike the old id-based storage where the referenced
/// entity's id never changed across a rename.
/// </summary>
public class WorkspaceRepositoryJobTaskRepointTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DatatealDbContext _db;
    private readonly WorkspaceRepository _repository;
    private readonly Guid _workspaceId = Guid.NewGuid();

    public WorkspaceRepositoryJobTaskRepointTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<DatatealDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new DatatealDbContext(options);
        _db.Database.EnsureCreated();
        _repository = new WorkspaceRepository(_db);

        _db.Workspaces.Add(new Datateal.Core.Workspaces.Workspace { Id = _workspaceId, Name = "Test Workspace" });
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private Job AddJobWithNotebookTask(string notebookPath)
    {
        var job = new Job { Id = Guid.NewGuid(), WorkspaceId = _workspaceId, Name = $"job-{Guid.NewGuid():N}" };
        job.Tasks.Add(new NotebookTask { Id = Guid.NewGuid(), JobId = job.Id, Name = "task", NotebookPath = notebookPath });
        _db.Jobs.Add(job);
        _db.SaveChanges();
        return job;
    }

    private Job AddJobWithQueryTask(string queryPath)
    {
        var job = new Job { Id = Guid.NewGuid(), WorkspaceId = _workspaceId, Name = $"job-{Guid.NewGuid():N}" };
        job.Tasks.Add(new SqlQueryTask { Id = Guid.NewGuid(), JobId = job.Id, Name = "task", QueryPath = queryPath });
        _db.Jobs.Add(job);
        _db.SaveChanges();
        return job;
    }

    private string? GetStoredNotebookPath(Guid jobId) =>
        _db.JobTasks.OfType<NotebookTask>().AsNoTracking().Single(t => t.JobId == jobId).NotebookPath;

    private string? GetStoredQueryPath(Guid jobId) =>
        _db.JobTasks.OfType<SqlQueryTask>().AsNoTracking().Single(t => t.JobId == jobId).QueryPath;

    [Fact]
    public async Task UpdateNotebookAsync_Rename_RepointsReferencingJobTask()
    {
        var notebook = await _repository.CreateNotebookAsync(_workspaceId, "old_name", "print(1)", folderId: null);
        var job = AddJobWithNotebookTask("old_name");

        await _repository.UpdateNotebookAsync(_workspaceId, notebook.Id, "new_name", "print(1)", folderId: null);

        Assert.Equal("new_name", GetStoredNotebookPath(job.Id));
    }

    [Fact]
    public async Task UpdateNotebookAsync_ContentOnlyChange_LeavesJobTaskPathUntouched()
    {
        var notebook = await _repository.CreateNotebookAsync(_workspaceId, "same_name", "print(1)", folderId: null);
        var job = AddJobWithNotebookTask("same_name");

        await _repository.UpdateNotebookAsync(_workspaceId, notebook.Id, "same_name", "print(2)", folderId: null);

        Assert.Equal("same_name", GetStoredNotebookPath(job.Id));
    }

    [Fact]
    public async Task UpdateQueryAsync_Rename_RepointsReferencingJobTask()
    {
        var query = await _repository.CreateQueryAsync(_workspaceId, "old_query", "select 1", folderId: null, null, null, null, null);
        var job = AddJobWithQueryTask("old_query");

        await _repository.UpdateQueryAsync(_workspaceId, query.Id, "new_query", "select 1", folderId: null, null, null, null, null);

        Assert.Equal("new_query", GetStoredQueryPath(job.Id));
    }

    [Fact]
    public async Task UpdateFolderAsync_Rename_RepointsJobTasksForAllNestedNotebooksAndQueries()
    {
        var folder = await _repository.CreateFolderAsync(_workspaceId, "old_folder", parentId: null);
        var notebook = await _repository.CreateNotebookAsync(_workspaceId, "notebook1", "print(1)", folder.Id);
        var query = await _repository.CreateQueryAsync(_workspaceId, "query1", "select 1", folder.Id, null, null, null, null);

        var notebookJob = AddJobWithNotebookTask("old_folder/notebook1");
        var queryJob = AddJobWithQueryTask("old_folder/query1");

        await _repository.UpdateFolderAsync(_workspaceId, folder.Id, "new_folder", parentId: null);

        Assert.Equal("new_folder/notebook1", GetStoredNotebookPath(notebookJob.Id));
        Assert.Equal("new_folder/query1", GetStoredQueryPath(queryJob.Id));
    }

    [Fact]
    public async Task UpdateNotebookAsync_Rename_DoesNotAffectJobTasksInOtherWorkspaces()
    {
        var otherWorkspaceId = Guid.NewGuid();
        _db.Workspaces.Add(new Datateal.Core.Workspaces.Workspace { Id = otherWorkspaceId, Name = "Other Workspace" });
        await _db.SaveChangesAsync();

        var notebook = await _repository.CreateNotebookAsync(_workspaceId, "shared_name", "print(1)", folderId: null);

        var otherJob = new Job { Id = Guid.NewGuid(), WorkspaceId = otherWorkspaceId, Name = $"job-{Guid.NewGuid():N}" };
        otherJob.Tasks.Add(new NotebookTask { Id = Guid.NewGuid(), JobId = otherJob.Id, Name = "task", NotebookPath = "shared_name" });
        _db.Jobs.Add(otherJob);
        await _db.SaveChangesAsync();

        await _repository.UpdateNotebookAsync(_workspaceId, notebook.Id, "renamed", "print(1)", folderId: null);

        // The other workspace's identically-pathed reference must be untouched.
        Assert.Equal("shared_name", GetStoredNotebookPath(otherJob.Id));
    }
}
