namespace Datateal.Orchestrator.Core;

/// <summary>
/// Provides the workspace the current request is acting within. Populated for HTTP
/// requests from the <c>X-Datateal-Workspace</c> header forwarded by the UI server.
/// Background/system callers (scheduler, recovery, warm-pool manager) have no workspace
/// in scope and read <see cref="CurrentWorkspaceId"/> as <c>null</c>.
/// </summary>
public interface IWorkspaceContext
{
    Guid? CurrentWorkspaceId { get; }

    Guid RequireWorkspaceId();
}
