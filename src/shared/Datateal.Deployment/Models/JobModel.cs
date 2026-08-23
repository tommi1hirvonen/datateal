using YamlDotNet.Serialization;

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
    // YamlDotNet's global DefaultValuesHandling.OmitDefaults compares against default(bool)
    // (false), not this property's meaningful default (true) — without this override, a
    // disabled job (IsEnabled = false) would serialize with the field omitted, and re-import
    // would silently re-enable it via the property initializer. Force it to always serialize.
    [YamlMember(DefaultValuesHandling = DefaultValuesHandling.OmitNull)]
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
