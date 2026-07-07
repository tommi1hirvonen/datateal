namespace Datateal.Ui.Shared.ApiTokens;

/// <summary>
/// A managed API token. The secret value is never included — only the display
/// <see cref="TokenPrefix"/> and metadata.
/// </summary>
public record ApiTokenDto(
    Guid Id,
    string Name,
    string TokenPrefix,
    string ScopeType,
    Guid? WorkspaceId,
    string? WorkspaceName,
    List<string> Roles,
    Guid? CreatedByUserId,
    Guid? ActingUserId,
    string? ActingUserDisplayName,
    DateTime ValidFrom,
    DateTime? ValidTo,
    bool IsRevoked,
    DateTime? LastUsedAt,
    DateTime CreatedAt);

public record CreateApiTokenRequest(
    string Name,
    string ScopeType,
    Guid? WorkspaceId,
    List<string> Roles,
    DateTime? ValidTo,
    Guid? ActingUserId = null);

/// <summary>
/// Returned once, on creation. <see cref="Value"/> is the plaintext token and is never
/// retrievable again.
/// </summary>
public record CreateApiTokenResponse(
    ApiTokenDto Token,
    string Value);
