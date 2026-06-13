namespace Datateal.Ui.Shared.Workspaces;

/// <summary>
/// Resolves the workspace the current request/UI session is acting within.
/// Server implementation reads the <c>workspaceId</c> route parameter present on all
/// workspace-scoped routes (<c>api/workspaces/{workspaceId:guid}/...</c>).
/// The WASM client implementation reads the active-workspace UI state.
/// </summary>
public interface IActiveWorkspaceAccessor
{
    /// <summary>The active workspace id, or <c>null</c> when none is in scope.</summary>
    Guid? ActiveWorkspaceId { get; }
}
