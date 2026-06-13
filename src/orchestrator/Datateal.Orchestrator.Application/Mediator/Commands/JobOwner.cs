namespace Datateal.Orchestrator.Application.Mediator.Commands;

/// <summary>
/// Shared constants for job effective-owner validation.
/// </summary>
internal static class JobOwner
{
    /// <summary>
    /// Error surfaced when a create/update/import request carries no resolvable acting user. The owner
    /// is stamped server-side from the authenticated principal (UI proxy) or the acting-user header
    /// (direct integrations); its absence indicates the caller is not a provisioned application user.
    /// </summary>
    public const string MissingOwnerMessage =
        "A job owner is required but the acting user could not be resolved. Ensure the request is made " +
        "by a provisioned application user.";
}
