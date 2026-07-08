using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Datateal.Orchestrator.Application.Yaml;

/// <summary>
/// Root YAML model for job import/export. Uses snake_case keys.
/// </summary>
public class YamlJobModel
{
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public int MaxConcurrentRuns { get; set; } = 1;
    public List<YamlParameterModel> Parameters { get; set; } = [];
    public List<YamlNodePoolModel> NodePools { get; set; } = [];
    public List<YamlTaskModel> Tasks { get; set; } = [];
    public List<YamlScheduleModel> Schedules { get; set; } = [];
}

public class YamlParameterModel
{
    public string Name { get; set; } = "";
    public string? DefaultValue { get; set; }
    public bool Required { get; set; }
    public string? Description { get; set; }
}

public class YamlNodePoolModel
{
    public string Name { get; set; } = "";
    public string VmSize { get; set; } = "";
    public string? KernelRequirements { get; set; }
    public string? Description { get; set; }
}

public class YamlTaskModel
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
    public List<YamlDependencyModel> Dependencies { get; set; } = [];
}

public class YamlDependencyModel
{
    public string Task { get; set; } = "";
    /// <summary><c>on_success</c>, <c>on_failure</c>, <c>on_completion</c>, <c>on_skip</c>.</summary>
    public string Condition { get; set; } = "on_success";
}

public class YamlScheduleModel
{
    public string Name { get; set; } = "";
    public string Cron { get; set; } = "";
    public string? TimeZone { get; set; }
    public Dictionary<string, string>? Parameters { get; set; }
}
