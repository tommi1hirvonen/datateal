using System.Text.Json.Serialization;
using Datateal.Core.Nodes;

namespace Datateal.Orchestrator.Core.Entities;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$poolType")]
[JsonDerivedType(typeof(JobNodePoolConfig), "Job")]
[JsonDerivedType(typeof(InteractiveNodePoolConfig), "Interactive")]
public abstract class NodePoolConfig
{
    public Guid Id { get; set; }

    /// <summary>Owning workspace.</summary>
    public Guid WorkspaceId { get; set; }

    public required string Name { get; set; }
    public NodePoolType PoolType { get; protected set; }
    public required string VmSize { get; set; }
    public TimeSpan? KernelIdleTimeout { get; set; }
    public TimeSpan? NodeIdleTimeout { get; set; }
    public string? KernelRequirements { get; set; }
    public string? Description { get; set; }
    public List<Guid>? WheelPackageIds { get; set; }
    public List<Guid>? EnvironmentVariableIds { get; set; }
    public List<Guid>? SecretIds { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
