namespace Datateal.Core.Environment;

public class EnvironmentVariable
{
    public Guid Id { get; set; }

    /// <summary>Owning workspace.</summary>
    public Guid WorkspaceId { get; set; }

    public required string Key { get; set; }
    public required string Value { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
