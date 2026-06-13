using Datateal.Ui.Shared.Workspaces;

namespace Datateal.Ui.Server.Auth;

/// <summary>
/// Server-side <see cref="IActiveWorkspaceAccessor"/>. Resolves the active workspace from
/// the <c>workspaceId</c> route parameter, which is present on all workspace-scoped API
/// routes (<c>api/workspaces/{workspaceId:guid}/...</c>).
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

            if (context.Request.RouteValues.TryGetValue("workspaceId", out var routeValue)
                && Guid.TryParse(routeValue?.ToString(), out var routeId))
                return routeId;

            return null;
        }
    }
}
