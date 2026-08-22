using Datateal.Orchestrator.Core.Entities;
using Datateal.Orchestrator.Core.Enums;
using Datateal.Orchestrator.Core.Repositories;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Datateal.Orchestrator.Application.Yaml;

/// <summary>
/// Converts a <see cref="Job"/> entity into YAML text using snake_case keys.
/// </summary>
public class YamlJobSerializer(IJobRepository jobRepository)
{
    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
        .Build();

    public async Task<string> SerializeAsync(Job job, CancellationToken ct = default)
    {
        var model = new YamlJobModel
        {
            Name = job.Name,
            Description = job.Description,
            MaxConcurrentRuns = job.MaxConcurrentRuns,
        };

        // Parameters
        foreach (var p in job.Parameters)
        {
            model.Parameters.Add(new YamlParameterModel
            {
                Name = p.Name,
                DefaultValue = p.DefaultValue,
                Required = p.IsRequired,
                Description = p.Description,
            });
        }

        // Build task-id-to-name lookup for dependency resolution
        var taskNameById = job.Tasks.ToDictionary(t => t.Id, t => t.Name);

        // Tasks
        foreach (var task in job.Tasks)
        {
            var yamlTask = new YamlTaskModel
            {
                Name = task.Name,
                MaxRetries = task.MaxRetries,
                RetryInterval = task.RetryInterval != TimeSpan.Zero
                    ? task.RetryInterval.ToString()
                    : null,
                Timeout = task.Timeout?.ToString(),
            };

            switch (task)
            {
                case NotebookTask nb:
                    yamlTask.Type = "notebook";
                    yamlTask.NotebookPath = nb.NotebookPath;
                    yamlTask.NodePoolRef = nb.NodePoolRef;
                    yamlTask.Parameters = nb.Parameters;
                    break;
                case SqlQueryTask sq:
                    yamlTask.Type = "sql_query";
                    yamlTask.QueryPath = sq.QueryPath;
                    yamlTask.NodePoolRef = sq.NodePoolRef;
                    yamlTask.Parameters = sq.Parameters;
                    break;
                case SubJobTask sj:
                    yamlTask.Type = "sub_job";
                    var subJob = await jobRepository.GetJobAsync(sj.SubJobId, ct);
                    yamlTask.JobName = subJob?.Name;
                    yamlTask.Parameters = sj.Parameters;
                    break;
            }

            foreach (var dep in task.Dependencies)
            {
                yamlTask.Dependencies.Add(new YamlDependencyModel
                {
                    Task = taskNameById.TryGetValue(dep.DependsOnTaskId, out var name) ? name : dep.DependsOnTaskId.ToString(),
                    Condition = dep.Condition switch
                    {
                        DependencyCondition.OnSuccess => "on_success",
                        DependencyCondition.OnFailure => "on_failure",
                        DependencyCondition.OnCompletion => "on_completion",
                        DependencyCondition.OnSkip => "on_skip",
                        _ => "on_success",
                    },
                });
            }

            model.Tasks.Add(yamlTask);
        }

        // Schedules
        foreach (var s in job.Schedules)
        {
            model.Schedules.Add(new YamlScheduleModel
            {
                Name = s.Name,
                Cron = s.CronExpression,
                TimeZone = s.TimeZone,
                Parameters = s.Parameters,
            });
        }

        return Serializer.Serialize(model);
    }
}
