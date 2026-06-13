using Datateal.Ui.Shared.Workspaces;

namespace Datateal.Ui.Client.Services;

/// <summary>
/// Adds the <c>X-Datateal-Workspace</c> header (the active workspace id) to every API
/// request so the server can scope queries and authorization to that workspace.
/// </summary>
internal sealed class WorkspaceHeaderHandler(IActiveWorkspaceAccessor activeWorkspace) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (activeWorkspace.ActiveWorkspaceId is { } id)
        {
            request.Headers.Remove(WorkspaceRoleClaims.HeaderName);
            request.Headers.TryAddWithoutValidation(WorkspaceRoleClaims.HeaderName, id.ToString());
        }

        return base.SendAsync(request, cancellationToken);
    }
}
