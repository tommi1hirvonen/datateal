namespace DuckHouse.Orchestrator.Core.Entities;

/// <summary>
/// Abstract base for task runs that execute on a provisioned compute node and kernel
/// (Notebook and SQL query tasks).
/// </summary>
public abstract class ComputeTaskRun : TaskRun
{
    public string? NodeName { get; set; }
    public string? KernelId { get; set; }
    public string? OutputJson { get; set; }
}
