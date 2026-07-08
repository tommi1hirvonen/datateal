using Datateal.Deployment.Serialization;
using Datateal.Deployment.Models;

namespace Datateal.Core.Tests.Deployment;

/// <summary>
/// Verifies that the job YAML format uses snake_case keys and the correct
/// task-type / dependency-condition value strings after the camelCase→snake_case
/// refactor.
/// </summary>
public class JobYamlRegressionTests
{
    // ── Snake_case key test via BundleYaml ────────────────────────────────────

    [Fact]
    public void JobModel_SerializesToSnakeCase()
    {
        var job = new JobModel
        {
            Name = "nightly_etl",
            Description = "Nightly pipeline",
            MaxConcurrentRuns = 2,
            Tasks =
            [
                new JobTaskModel
                {
                    Name = "load_data",
                    Type = "notebook",
                    NotebookPath = "etl/load_data",
                    NodePoolRef = "etl-pool",
                    Dependencies =
                    [
                        new JobTaskDependencyModel { Task = "validate", Condition = "on_success" }
                    ],
                },
                new JobTaskModel
                {
                    Name = "run_query",
                    Type = "sql_query",
                    QueryPath = "reports/summary",
                    NodePoolRef = "etl-pool",
                },
                new JobTaskModel
                {
                    Name = "invoke_child",
                    Type = "sub_job",
                    JobName = "child_job",
                },
            ],
            Schedules =
            [
                new JobScheduleModel { Name = "daily", Cron = "0 0 6 * * ?", TimeZone = "UTC" }
            ],
        };

        var yaml = BundleYaml.Serialize(job);

        // Keys must be snake_case
        Assert.Contains("name:", yaml);
        Assert.Contains("max_concurrent_runs:", yaml);
        Assert.Contains("notebook_path:", yaml);
        Assert.Contains("query_path:", yaml);
        Assert.Contains("job_name:", yaml);
        Assert.Contains("node_pool_ref:", yaml);

        // Task type values must be snake_case
        Assert.Contains("type: notebook", yaml);
        Assert.Contains("type: sql_query", yaml);
        Assert.Contains("type: sub_job", yaml);

        // Dependency condition values must be snake_case
        Assert.Contains("condition: on_success", yaml);
    }

    [Fact]
    public void JobModel_DeserializesFromSnakeCase()
    {
        const string yaml = """
            name: nightly_etl
            max_concurrent_runs: 3
            tasks:
              - name: load_data
                type: sql_query
                query_path: reports/summary
                node_pool_ref: etl-pool
                dependencies:
                  - task: validate
                    condition: on_failure
            schedules:
              - name: morning
                cron: "0 0 8 * * ?"
                time_zone: UTC
            """;

        var model = BundleYaml.Deserialize<JobModel>(yaml);

        Assert.Equal("nightly_etl", model.Name);
        Assert.Equal(3, model.MaxConcurrentRuns);
        Assert.Single(model.Tasks);
        Assert.Equal("sql_query", model.Tasks[0].Type);
        Assert.Equal("reports/summary", model.Tasks[0].QueryPath);
        Assert.Equal("etl-pool", model.Tasks[0].NodePoolRef);
        Assert.Equal("on_failure", model.Tasks[0].Dependencies[0].Condition);
        Assert.Single(model.Schedules);
        Assert.Equal("morning", model.Schedules[0].Name);
        Assert.Equal("UTC", model.Schedules[0].TimeZone);
    }

    [Fact]
    public void BundleYaml_RoundTrip_IsStable()
    {
        var original = new JobModel
        {
            Name = "test_job",
            MaxConcurrentRuns = 1,
            Parameters = [new JobParameterModel { Name = "env", Required = true }],
            Tasks =
            [
                new JobTaskModel
                {
                    Name = "step1",
                    Type = "notebook",
                    NotebookPath = "etl/step1",
                    NodePoolRef = "pool1",
                }
            ],
        };

        var yaml1 = BundleYaml.Serialize(original);
        var deserialized = BundleYaml.Deserialize<JobModel>(yaml1);
        var yaml2 = BundleYaml.Serialize(deserialized);

        Assert.Equal(yaml1, yaml2);
    }
}
