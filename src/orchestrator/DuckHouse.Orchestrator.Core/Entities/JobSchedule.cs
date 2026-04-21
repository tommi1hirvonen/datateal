using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace DuckHouse.Orchestrator.Core.Entities;

public class JobSchedule
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    [JsonIgnore]
    public Job? Job { get; set; }

    public required string CronExpression { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string? TimeZone { get; set; }
    public Dictionary<string, string>? Parameters { get; set; }

    /// <summary>
    /// Not persisted to the database. Populated from the Quartz scheduler at query time.
    /// </summary>
    [NotMapped]
    public DateTime? NextFireTime { get; set; }
}
