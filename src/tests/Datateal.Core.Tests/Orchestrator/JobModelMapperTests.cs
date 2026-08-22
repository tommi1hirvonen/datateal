using Datateal.Core.Nodes;
using Datateal.Deployment.Models;
using Datateal.Orchestrator.Application;
using Datateal.Orchestrator.Core.Entities;
using Datateal.Orchestrator.Core.Interfaces;
using Datateal.Orchestrator.Core.Repositories;
using Xunit;

namespace Datateal.Core.Tests.Orchestrator;

/// <summary>
/// Regression coverage for the fix that stores <c>NotebookTask</c>/<c>SqlQueryTask</c>/<c>SubJobTask</c>
/// references as a workspace path or job name rather than a persisted Guid id. A workspace bundle
/// deploy that moves or renames a notebook/query (or a job) recreates the underlying row with a
/// brand-new id (see <c>WorkspaceNotebookMapper</c>/<c>WorkspaceQueryMapper</c>/<c>JobModelMapper.NaturalKey</c>,
/// all keyed purely by path/name). Storing a path/name instead of an id means a job task's
/// reference is re-resolved fresh on every use instead of freezing to a specific id that a later
/// rename can silently orphan.
/// </summary>
public class JobModelMapperTests
{
    [Fact]
    public async Task ToModelAsync_CopiesNotebookAndQueryPathsDirectly_WithoutResolvingAnyId()
    {
        // The fake reader throws on any resolution call — proving ToModelAsync (used for bundle
        // export and YAML export) never needs to touch the workspace to report a task's path.
        var reader = new ThrowingWorkspaceReader();
        var mapper = new JobModelMapper(reader, new FakeJobRepository(), new FakeNodePoolConfigRepository());

        var job = new Job
        {
            Id = Guid.NewGuid(),
            WorkspaceId = Guid.NewGuid(),
            Name = "ETL Job",
            Tasks =
            [
                new NotebookTask { Id = Guid.NewGuid(), Name = "run-notebook", NotebookPath = "etl/load_data", NodePoolRef = "pool-a" },
                new SqlQueryTask { Id = Guid.NewGuid(), Name = "run-query", QueryPath = "reports/summary", NodePoolRef = "pool-a" },
            ],
        };

        var model = await mapper.ToModelAsync(job, CancellationToken.None);

        Assert.Equal("etl/load_data", model.Tasks.Single(t => t.Name == "run-notebook").NotebookPath);
        Assert.Equal("reports/summary", model.Tasks.Single(t => t.Name == "run-query").QueryPath);
    }

    [Fact]
    public async Task ToCreateRequestAsync_Throws_WhenNotebookPathNoLongerExists()
    {
        // Simulates a job that still references a notebook by its old path after the notebook
        // was moved elsewhere by a workspace bundle deploy (or deleted).
        var reader = new FakeWorkspaceReader(); // empty — nothing resolves
        var mapper = new JobModelMapper(reader, new FakeJobRepository(), new FakeNodePoolConfigRepository(["pool-a"]));
        var bundleModel = NotebookJobModel("folderA/notebook1", "pool-a");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            mapper.ToCreateRequestAsync(Guid.NewGuid(), Guid.NewGuid(), bundleModel, CancellationToken.None));

        Assert.Contains("folderA/notebook1", ex.Message);
    }

    [Fact]
    public async Task ToCreateRequestAsync_StoresPathNotId_ForNotebookAndQueryTasks()
    {
        var reader = new FakeWorkspaceReader();
        reader.Notebooks["folderb/notebook1"] = Guid.NewGuid();
        reader.Queries["reports/summary"] = Guid.NewGuid();

        var mapper = new JobModelMapper(reader, new FakeJobRepository(), new FakeNodePoolConfigRepository(["pool-a"]));
        var workspaceId = Guid.NewGuid();

        var bundleModel = new JobModel
        {
            Name = "ETL Job",
            Tasks =
            [
                new JobTaskModel { Name = "run-notebook", Type = "notebook", NotebookPath = "folderB/notebook1", NodePoolRef = "pool-a" },
                new JobTaskModel { Name = "run-query", Type = "sql_query", QueryPath = "reports/summary", NodePoolRef = "pool-a" },
            ],
        };

        var request = await mapper.ToCreateRequestAsync(workspaceId, Guid.NewGuid(), bundleModel, CancellationToken.None);

        var notebookTask = request.Tasks!.Single(t => t.Name == "run-notebook");
        var queryTask = request.Tasks!.Single(t => t.Name == "run-query");
        Assert.Equal("folderB/notebook1", notebookTask.NotebookPath);
        Assert.Equal("reports/summary", queryTask.QueryPath);
    }

    [Fact]
    public async Task ToCreateRequestAsync_SucceedsAfterRename_WhenBundleAndJobAreRedeployedTogether()
    {
        // The notebook used to live at folderA/notebook1 (old id A). A workspace bundle deploy
        // moved it to folderB/notebook1, recreating it with a brand-new id B. A job bundle that is
        // redeployed in the same operation, referencing the *new* path, must resolve cleanly
        // regardless of what the old id was — proving no id is ever frozen into the stored
        // reference that a rename could later orphan.
        var reader = new FakeWorkspaceReader();
        var newNotebookId = Guid.NewGuid(); // simulates the row recreated by the rename
        reader.Notebooks["folderb/notebook1"] = newNotebookId;

        var mapper = new JobModelMapper(reader, new FakeJobRepository(), new FakeNodePoolConfigRepository(["pool-a"]));
        var bundleModel = NotebookJobModel("folderB/notebook1", "pool-a");

        var request = await mapper.ToCreateRequestAsync(Guid.NewGuid(), Guid.NewGuid(), bundleModel, CancellationToken.None);

        Assert.Equal("folderB/notebook1", request.Tasks!.Single().NotebookPath);
    }

    [Fact]
    public async Task ToModelAsync_CopiesSubJobNameDirectly_WithoutResolvingAnyId()
    {
        // FakeJobRepository.GetJobAsync always returns null — proving ToModelAsync no longer needs
        // to look the sub-job up by id (the old code called jobRepository.GetJobAsync(subJob.SubJobId)).
        var mapper = new JobModelMapper(new ThrowingWorkspaceReader(), new FakeJobRepository(), new FakeNodePoolConfigRepository());

        var job = new Job
        {
            Id = Guid.NewGuid(),
            WorkspaceId = Guid.NewGuid(),
            Name = "Orchestrating Job",
            Tasks = [new SubJobTask { Id = Guid.NewGuid(), Name = "run-child", SubJobName = "Child Job" }],
        };

        var model = await mapper.ToModelAsync(job, CancellationToken.None);

        Assert.Equal("Child Job", model.Tasks.Single().JobName);
    }

    [Fact]
    public async Task ToCreateRequestAsync_Throws_WhenSubJobNameNoLongerExists()
    {
        // Simulates a job that still references a sub-job by its old name after that job was
        // renamed (or deleted).
        var jobRepository = new FakeJobRepository(); // empty — nothing resolves
        var mapper = new JobModelMapper(new FakeWorkspaceReader(), jobRepository, new FakeNodePoolConfigRepository());
        var bundleModel = new JobModel
        {
            Name = "Orchestrating Job",
            Tasks = [new JobTaskModel { Name = "run-child", Type = "sub_job", JobName = "Old Child Job Name" }],
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            mapper.ToCreateRequestAsync(Guid.NewGuid(), Guid.NewGuid(), bundleModel, CancellationToken.None));

        Assert.Contains("Old Child Job Name", ex.Message);
    }

    [Fact]
    public async Task ToCreateRequestAsync_StoresNameNotId_ForSubJobTasks()
    {
        var workspaceId = Guid.NewGuid();
        var jobRepository = new FakeJobRepository();
        jobRepository.JobsByName["Child Job"] = new Job { Id = Guid.NewGuid(), WorkspaceId = workspaceId, Name = "Child Job" };
        var mapper = new JobModelMapper(new FakeWorkspaceReader(), jobRepository, new FakeNodePoolConfigRepository());

        var bundleModel = new JobModel
        {
            Name = "Orchestrating Job",
            Tasks = [new JobTaskModel { Name = "run-child", Type = "sub_job", JobName = "Child Job" }],
        };

        var request = await mapper.ToCreateRequestAsync(workspaceId, Guid.NewGuid(), bundleModel, CancellationToken.None);

        Assert.Equal("Child Job", request.Tasks!.Single().SubJobName);
    }

    private static JobModel NotebookJobModel(string notebookPath, string nodePoolRef) => new()
    {
        Name = "ETL Job",
        Tasks = [new JobTaskModel { Name = "run-notebook", Type = "notebook", NotebookPath = notebookPath, NodePoolRef = nodePoolRef }],
    };

    private sealed class FakeWorkspaceReader : IWorkspaceReader
    {
        public Dictionary<string, Guid> Notebooks { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, Guid> Queries { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<WorkspaceItemContent?> GetNotebookContentAsync(Guid notebookId, CancellationToken ct = default) =>
            Task.FromResult<WorkspaceItemContent?>(new WorkspaceItemContent(notebookId, "Notebook", ""));

        public Task<WorkspaceItemContent?> GetQueryContentAsync(Guid queryId, CancellationToken ct = default) =>
            Task.FromResult<WorkspaceItemContent?>(new WorkspaceItemContent(queryId, "Query", ""));

        public Task<Guid?> ResolveNotebookIdByPathAsync(Guid workspaceId, string path, CancellationToken ct = default) =>
            Task.FromResult(Notebooks.TryGetValue(path.Trim('/'), out var id) ? id : (Guid?)null);

        public Task<Guid?> ResolveQueryIdByPathAsync(Guid workspaceId, string path, CancellationToken ct = default) =>
            Task.FromResult(Queries.TryGetValue(path.Trim('/'), out var id) ? id : (Guid?)null);

        public Task<IReadOnlyList<string>> GetWorkspaceItemCatalogNamesAsync(Guid itemId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>([]);
    }

    private sealed class ThrowingWorkspaceReader : IWorkspaceReader
    {
        public Task<WorkspaceItemContent?> GetNotebookContentAsync(Guid notebookId, CancellationToken ct = default) =>
            throw new InvalidOperationException("Should not be called by ToModelAsync.");

        public Task<WorkspaceItemContent?> GetQueryContentAsync(Guid queryId, CancellationToken ct = default) =>
            throw new InvalidOperationException("Should not be called by ToModelAsync.");

        public Task<Guid?> ResolveNotebookIdByPathAsync(Guid workspaceId, string path, CancellationToken ct = default) =>
            throw new InvalidOperationException("Should not be called by ToModelAsync.");

        public Task<Guid?> ResolveQueryIdByPathAsync(Guid workspaceId, string path, CancellationToken ct = default) =>
            throw new InvalidOperationException("Should not be called by ToModelAsync.");

        public Task<IReadOnlyList<string>> GetWorkspaceItemCatalogNamesAsync(Guid itemId, CancellationToken ct = default) =>
            throw new InvalidOperationException("Should not be called by ToModelAsync.");
    }

    private sealed class FakeJobRepository : IJobRepository
    {
        public Dictionary<string, Job> JobsByName { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Task<IReadOnlyList<Job>> GetJobsAsync(Guid workspaceId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Job>>([]);

        public Task<Job?> GetJobAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Job?>(null);

        public Task<Job?> GetJobDetailAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Job?>(null);

        public Task<Job?> GetJobByNameAsync(string name, Guid workspaceId, CancellationToken cancellationToken = default) =>
            Task.FromResult(JobsByName.TryGetValue(name, out var job) ? job : null);

        public Task<Job> CreateJobAsync(Job job, CancellationToken cancellationToken = default) =>
            Task.FromResult(job);

        public Task<Job?> UpdateJobAsync(Job job, CancellationToken cancellationToken = default) =>
            Task.FromResult<Job?>(job);

        public Task<bool> DeleteJobAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }

    private sealed class FakeNodePoolConfigRepository(IEnumerable<string>? poolNames = null) : INodePoolConfigRepository
    {
        private readonly HashSet<string> _poolNames = new(poolNames ?? [], StringComparer.OrdinalIgnoreCase);

        public Task<IReadOnlyList<NodePoolConfig>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<NodePoolConfig>>([]);

        public Task<IReadOnlyList<NodePoolConfig>> GetByWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<NodePoolConfig>>([]);

        public Task<NodePoolConfig?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<NodePoolConfig?>(null);

        public Task<NodePoolConfig?> GetByNameAsync(string name, Guid workspaceId, CancellationToken cancellationToken = default) =>
            Task.FromResult<NodePoolConfig?>(_poolNames.Contains(name)
                ? new JobNodePoolConfig { Id = Guid.NewGuid(), WorkspaceId = workspaceId, Name = name, VmSize = "Standard_D2s_v3" }
                : null);

        public Task<NodePoolConfig> CreateAsync(NodePoolConfig config, CancellationToken cancellationToken = default) =>
            Task.FromResult(config);

        public Task<NodePoolConfig?> UpdateAsync(NodePoolConfig config, CancellationToken cancellationToken = default) =>
            Task.FromResult<NodePoolConfig?>(config);

        public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(true);
    }
}
