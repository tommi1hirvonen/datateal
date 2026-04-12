namespace DuckHouse.Orchestrator.Core.Entities;

public class NodePoolConfig
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string VmSize { get; set; }
    public TimeSpan? KernelIdleTimeout { get; set; }
    public TimeSpan? NodeIdleTimeout { get; set; }
    public string? KernelRequirements { get; set; }
    public string? Description { get; set; }
    public List<Guid>? WheelPackageIds { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
