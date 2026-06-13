using Datateal.Orchestrator.Application.Validation;
using Datateal.Orchestrator.Core.Entities;
using Datateal.Orchestrator.Core.Enums;
using Datateal.Orchestrator.Core.Interfaces;
using Datateal.Orchestrator.Core.Repositories;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Datateal.Orchestrator.Application.Yaml;

/// <summary>
/// Deserializes YAML text into a <see cref="Job"/> entity, resolving workspace paths and
/// sub-job references from the database.
/// </summary>
public class YamlJobImporter(
    IWorkspaceReader workspaceReader,
    IJobRepository jobRepository,
    INodePoolConfigRepository nodePoolConfigRepository)
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public async Task<Job> ImportAsync(Guid workspaceId, string yaml, CancellationToken ct = default)
    {
        var model = Deserializer.Deserialize<YamlJobModel>(yaml)
            ?? throw new InvalidOperationException("Failed to parse YAML: the document is empty.");

        if (string.IsNullOrWhiteSpace(model.Name))
            throw new InvalidOperationException("Job name is required.");

        // Validate unique job name within the workspace.
        var nameConflict = await jobRepository.GetJobByNameAsync(model.Name, workspaceId, ct);
        if (nameConflict is not null)
            throw new JobNameConflictException(model.Name);

        // Create or update node pool configs
        foreach (var pool in model.NodePools)
        {
            if (string.IsNullOrWhiteSpace(pool.Name))
                throw new InvalidOperationException("Node pool name is required.");

            var existing = await nodePoolConfigRepository.GetByNameAsync(pool.Name, workspaceId, ct);
            if (existing is null)
            {
                await nodePoolConfigRepository.CreateAsync(new JobNodePoolConfig
                {
                    WorkspaceId = workspaceId,
                    Name = pool.Name,
                    VmSize = string.IsNullOrWhiteSpace(pool.VmSize) ? "Standard_D2s_v3" : pool.VmSize,
                    KernelRequirements = pool.KernelRequirements,
                    Description = pool.Description,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                }, ct);
            }
        }

        var job = new Job
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            Name = model.Name,
            Description = model.Description,
            MaxConcurrentRuns = model.MaxConcurrentRuns > 0 ? model.MaxConcurrentRuns : 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        // Parameters
        foreach (var p in model.Parameters)
        {
            if (string.IsNullOrWhiteSpace(p.Name))
                throw new InvalidOperationException("Parameter name is required.");

            ParameterNameValidator.Validate(p.Name);

            job.Parameters.Add(new JobParameter
            {
                Id = Guid.NewGuid(),
                JobId = job.Id,
                Name = p.Name,
                DefaultValue = p.DefaultValue,
                IsRequired = p.Required,
                Description = p.Description,
            });
        }

        // Build tasks — first pass: create task entities
        var tasksByName = new Dictionary<string, JobTask>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in model.Tasks)
        {
            if (string.IsNullOrWhiteSpace(t.Name))
                throw new InvalidOperationException("Task name is required.");

            JobTask task = t.Type.ToLowerInvariant() switch
            {
                "notebook" => new NotebookTask
                {
                    Id = Guid.NewGuid(),
                    JobId = job.Id,
                    Name = t.Name,
                    MaxRetries = t.MaxRetries,
                    RetryInterval = ParseTimeSpan(t.RetryInterval, TimeSpan.FromSeconds(30)),
                    Timeout = ParseNullableTimeSpan(t.Timeout),
                    NotebookId = await ResolveNotebookAsync(t.NotebookPath, ct),
                    NodePoolRef = string.IsNullOrWhiteSpace(t.NodePoolRef)
                        ? throw new InvalidOperationException($"NodePoolRef is required for notebook task '{t.Name}'.")
                        : t.NodePoolRef,
                    Parameters = t.Parameters,
                },
                "sqlquery" or "sql" => new SqlQueryTask
                {
                    Id = Guid.NewGuid(),
                    JobId = job.Id,
                    Name = t.Name,
                    MaxRetries = t.MaxRetries,
                    RetryInterval = ParseTimeSpan(t.RetryInterval, TimeSpan.FromSeconds(30)),
                    Timeout = ParseNullableTimeSpan(t.Timeout),
                    QueryId = await ResolveQueryAsync(t.QueryPath, ct),
                    NodePoolRef = string.IsNullOrWhiteSpace(t.NodePoolRef)
                        ? throw new InvalidOperationException($"NodePoolRef is required for SQL query task '{t.Name}'.")
                        : t.NodePoolRef,
                    Parameters = t.Parameters,
                },
                "subjob" => new SubJobTask
                {
                    Id = Guid.NewGuid(),
                    JobId = job.Id,
                    Name = t.Name,
                    MaxRetries = t.MaxRetries,
                    RetryInterval = ParseTimeSpan(t.RetryInterval, TimeSpan.FromSeconds(30)),
                    Timeout = ParseNullableTimeSpan(t.Timeout),
                    SubJobId = await ResolveSubJobAsync(workspaceId, t.JobName, ct),
                    Parameters = t.Parameters,
                },
                _ => throw new InvalidOperationException($"Unknown task type: '{t.Type}'."),
            };

            if (!tasksByName.TryAdd(t.Name, task))
                throw new InvalidOperationException($"Duplicate task name: '{t.Name}'.");

            job.Tasks.Add(task);
        }

        // Second pass: resolve dependencies
        for (var i = 0; i < model.Tasks.Count; i++)
        {
            var taskModel = model.Tasks[i];
            var task = job.Tasks[i];

            foreach (var dep in taskModel.Dependencies)
            {
                if (!tasksByName.TryGetValue(dep.Task, out var dependsOnTask))
                    throw new InvalidOperationException($"Task '{task.Name}' depends on unknown task '{dep.Task}'.");

                task.Dependencies.Add(new TaskDependency
                {
                    Id = Guid.NewGuid(),
                    TaskId = task.Id,
                    DependsOnTaskId = dependsOnTask.Id,
                    Condition = ParseCondition(dep.Condition),
                });
            }
        }

        DagValidator.Validate(job.Tasks);

        // Schedules
        foreach (var s in model.Schedules)
        {
            if (string.IsNullOrWhiteSpace(s.Cron))
                throw new InvalidOperationException("Schedule cron expression is required.");

            job.Schedules.Add(new JobSchedule
            {
                Id = Guid.NewGuid(),
                JobId = job.Id,
                CronExpression = s.Cron,
                TimeZone = s.TimeZone,
                Parameters = s.Parameters,
            });
        }

        return job;
    }

    private async Task<Guid> ResolveNotebookAsync(string? path, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException("Notebook path is required for notebook tasks.");

        var id = await workspaceReader.ResolveNotebookIdByPathAsync(path, ct)
            ?? throw new InvalidOperationException($"Notebook not found at path: '{path}'.");
        return id;
    }

    private async Task<Guid> ResolveQueryAsync(string? path, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException("Query path is required for SQL query tasks.");

        var id = await workspaceReader.ResolveQueryIdByPathAsync(path, ct)
            ?? throw new InvalidOperationException($"Query not found at path: '{path}'.");
        return id;
    }

    private async Task<Guid> ResolveSubJobAsync(Guid workspaceId, string? jobName, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(jobName))
            throw new InvalidOperationException("Job name is required for sub-job tasks.");

        var subJob = await jobRepository.GetJobByNameAsync(jobName, workspaceId, ct)
            ?? throw new InvalidOperationException($"Sub-job not found with name: '{jobName}'.");
        return subJob.Id;
    }

    private static DependencyCondition ParseCondition(string condition) => condition.ToLowerInvariant() switch
    {
        "onsuccess" => DependencyCondition.OnSuccess,
        "onfailure" => DependencyCondition.OnFailure,
        "oncompletion" => DependencyCondition.OnCompletion,
        "onskip" => DependencyCondition.OnSkip,
        _ => throw new InvalidOperationException($"Unknown dependency condition: '{condition}'."),
    };

    private static TimeSpan ParseTimeSpan(string? value, TimeSpan defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value)) return defaultValue;
        return TimeSpan.TryParse(value, out var result) ? result : defaultValue;
    }

    private static TimeSpan? ParseNullableTimeSpan(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return TimeSpan.TryParse(value, out var result) ? result : null;
    }
}
