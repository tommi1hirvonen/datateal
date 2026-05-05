using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using DuckHouse.Core.Orchestration;
using DuckHouse.Orchestrator.Core.Enums;

namespace DuckHouse.Orchestrator.Core.Entities;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "taskType")]
[JsonDerivedType(typeof(NotebookTaskRun), "Notebook")]
[JsonDerivedType(typeof(SqlQueryTaskRun), "SqlQuery")]
[JsonDerivedType(typeof(SubJobTaskRun), "SubJob")]
public abstract class TaskRun
{
    public Guid Id { get; set; }
    public Guid JobRunId { get; set; }
    [JsonIgnore]
    public JobRun? JobRun { get; set; }

    public Guid? TaskId { get; set; }
    [JsonIgnore]
    public JobTask? Task { get; set; }

    [JsonIgnore]
    public TaskType TaskType { get; protected set; }

    public string TaskName { get; set; } = string.Empty;
    public Dictionary<string, string>? Parameters { get; set; }

    public TaskRunStatus Status { get; set; }
    public int AttemptNumber { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public double? DurationMs { get; set; }

    [NotMapped]
    public List<string> DependencyTaskNames { get; set; } = [];
}
