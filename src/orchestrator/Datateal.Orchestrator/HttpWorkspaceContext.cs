using Datateal.Orchestrator.Core;

namespace Datateal.Orchestrator;

/// <summary>
/// Resolves the active workspace from the <c>X-Datateal-Workspace</c> header forwarded by
/// the UI server. Background/system callers run outside an HTTP request and observe no
/// workspace in scope.
/// </summary>
internal sealed class HttpWorkspaceContext(IHttpContextAccessor httpContextAccessor) : IWorkspaceContext
{
    private const string HeaderName = "X-Datateal-Workspace";

    public Guid? CurrentWorkspaceId
    {
        get
        {
            var context = httpContextAccessor.HttpContext;
            if (context is not null
                && context.Request.Headers.TryGetValue(HeaderName, out var header)
                && Guid.TryParse(header.ToString(), out var id))
                return id;

            return null;
        }
    }

    public Guid RequireWorkspaceId() => CurrentWorkspaceId
        ?? throw new InvalidOperationException(
            "No active workspace was supplied. The request must include the X-Datateal-Workspace header.");
}
