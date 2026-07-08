namespace Datateal.Deployment.Models;

/// <summary>
/// Job resource definition. Mirrors the orchestrator's job YAML shape but uses
/// snake_case via YamlDotNet's <c>UnderscoredNamingConvention</c>.
/// </summary>
public class JobModel
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public int MaxConcurrentRuns { get; set; } = 1;
    public bool IsEnabled { get; set; } = true;
    public List<JobParameterModel> Parameters { get; set; } = [];
    public List<JobTaskModel> Tasks { get; set; } = [];
    public List<JobScheduleModel> Schedules { get; set; } = [];
}

public class JobParameterModel
{
    public string Name { get; set; } = "";
    public string? DefaultValue { get; set; }
    public bool Required { get; set; }
    public string? Description { get; set; }
}

public class JobTaskModel
{
    public string Name { get; set; } = "";
    /// <summary><c>notebook</c>, <c>sql_query</c>, or <c>sub_job</c>.</summary>
    public string Type { get; set; } = "";
    public string? NotebookPath { get; set; }
    public string? QueryPath { get; set; }
    public string? JobName { get; set; }
    public string? NodePoolRef { get; set; }
    public int MaxRetries { get; set; }
    public string? RetryInterval { get; set; }
    public string? Timeout { get; set; }
    public Dictionary<string, string>? Parameters { get; set; }
    public List<JobTaskDependencyModel> Dependencies { get; set; } = [];
}

public class JobTaskDependencyModel
{
    public string Task { get; set; } = "";
    /// <summary><c>on_success</c>, <c>on_failure</c>, <c>on_completion</c>, <c>on_skip</c>.</summary>
    public string Condition { get; set; } = "on_success";
}

public class JobScheduleModel
{
    public string Name { get; set; } = "";
    public string Cron { get; set; } = "";
    public string? TimeZone { get; set; }
    public Dictionary<string, string>? Parameters { get; set; }
}
