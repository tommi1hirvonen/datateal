using Datateal.Ui.Shared.Workspaces;

namespace Datateal.Ui.Server.Auth;

/// <summary>
/// Server-side <see cref="IActiveWorkspaceAccessor"/>. Resolves the active workspace from
/// the <c>X-Datateal-Workspace</c> request header (sent by the WASM client on API calls),
/// falling back to a <c>workspaceId</c> route value or <c>ws</c> query parameter.
/// </summary>
public sealed class HttpActiveWorkspaceAccessor(IHttpContextAccessor httpContextAccessor)
    : IActiveWorkspaceAccessor
{
    public Guid? ActiveWorkspaceId
    {
        get
        {
            var context = httpContextAccessor.HttpContext;
            if (context is null)
                return null;

            if (context.Request.Headers.TryGetValue(WorkspaceRoleClaims.HeaderName, out var header)
                && Guid.TryParse(header.ToString(), out var headerId))
                return headerId;

            if (context.Request.RouteValues.TryGetValue("workspaceId", out var routeValue)
                && Guid.TryParse(routeValue?.ToString(), out var routeId))
                return routeId;

            if (context.Request.Query.TryGetValue("ws", out var query)
                && Guid.TryParse(query.ToString(), out var queryId))
                return queryId;

            return null;
        }
    }
}
