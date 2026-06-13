namespace Datateal.Core.Environment;

public class Secret
{
    public Guid Id { get; set; }

    /// <summary>Owning workspace.</summary>
    public Guid WorkspaceId { get; set; }

    public required string Key { get; set; }
    /// <summary>
    /// AES-encrypted via ASP.NET Data Protection API. Never stored in plaintext.
    /// </summary>
    public required string EncryptedValue { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
