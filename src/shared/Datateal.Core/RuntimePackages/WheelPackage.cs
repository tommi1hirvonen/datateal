namespace Datateal.Core.RuntimePackages;

public class WheelPackage
{
    public Guid Id { get; set; }

    /// <summary>Owning workspace.</summary>
    public Guid WorkspaceId { get; set; }

    public required string Name { get; set; }
    public required string FileName { get; set; }
    public required byte[] Data { get; set; }
    public long Size { get; set; }
    public DateTime UploadedAt { get; set; }
}
