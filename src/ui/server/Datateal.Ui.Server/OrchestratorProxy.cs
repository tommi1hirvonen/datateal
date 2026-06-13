using Datateal.Auth;
using Datateal.Auth.ApiKey;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Datateal.Ui.Server;

public static class OrchestratorProxy
{
    public static IServiceCollection AddOrchestratorProxy(this IServiceCollection services, IConfiguration configuration)
    {
        var baseAddress = configuration["Orchestrator:BaseAddress"]
            ?? throw new InvalidOperationException("Orchestrator:BaseAddress is not configured.");

        var apiKey = configuration["ServiceAuth:Orchestrator:ApiKey"]
            ?? throw new InvalidOperationException("ServiceAuth:Orchestrator:ApiKey is not configured.");

        services.AddHttpClient("Orchestrator", client =>
        {
            client.BaseAddress = new Uri(baseAddress);
        })
        .AddHttpMessageHandler(() => new ApiKeyDelegatingHandler(
            Options.Create(new ApiKeyDelegatingOptions { ApiKey = apiKey })));
        return services;
    }

    public static IEndpointRouteBuilder MapOrchestratorProxy(this IEndpointRouteBuilder endpoints)
    {
        endpoints.Map("/api/workspaces/{workspaceId:guid}/orchestrator/{**path}", async (
            HttpContext context,
            IHttpClientFactory clientFactory,
            IAuthorizationService authorizationService) =>
        {
            var path = context.Request.RouteValues["path"]?.ToString() ?? "";
            var policy = GetRequiredPolicy(path, context.Request.Method);

            if (policy is not null)
            {
                var authResult = await authorizationService.AuthorizeAsync(context.User, null, policy);
                if (!authResult.Succeeded)
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return;
                }
            }

            var client = clientFactory.CreateClient("Orchestrator");

            // Admin paths are tenant-level; all others are workspace-scoped and forwarded
            // with the workspaceId injected into the target URL path.
            var workspaceIdStr = context.Request.RouteValues["workspaceId"]?.ToString() ?? "";
            var forwardPath = path.StartsWith("admin", StringComparison.OrdinalIgnoreCase)
                ? $"/api/{path}"
                : $"/api/workspaces/{workspaceIdStr}/{path}";

            var targetUri = new Uri(client.BaseAddress!, $"{forwardPath}{context.Request.QueryString}");

            using var requestMessage = new HttpRequestMessage
            {
                Method = new HttpMethod(context.Request.Method),
                RequestUri = targetUri,
            };

            if (context.Request.ContentLength > 0 || context.Request.ContentType is not null)
            {
                requestMessage.Content = new StreamContent(context.Request.Body);
                if (context.Request.ContentType is not null)
                    requestMessage.Content.Headers.ContentType =
                        System.Net.Http.Headers.MediaTypeHeaderValue.Parse(context.Request.ContentType);
            }

            // Convey the authenticated acting user's application id so the orchestrator can record a
            // server-stamped effective owner on jobs. Stamped here from the validated principal — never
            // accepted from the inbound client request — so it cannot be spoofed.
            var actingUserId = context.User.FindFirst(DatatealClaimTypes.UserId)?.Value;
            if (actingUserId is not null)
                requestMessage.Headers.TryAddWithoutValidation(DatatealHeaders.ActingUser, actingUserId);

            using var responseMessage = await client.SendAsync(requestMessage, context.RequestAborted);

            context.Response.StatusCode = (int)responseMessage.StatusCode;

            foreach (var header in responseMessage.Content.Headers)
                context.Response.Headers[header.Key] = header.Value.ToArray();

            context.Response.Headers.Remove("transfer-encoding");

            if (responseMessage.Content.Headers.ContentLength != 0 &&
                context.Response.StatusCode != StatusCodes.Status204NoContent)
            {
                await responseMessage.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
            }
        }).RequireAuthorization();

        return endpoints;
    }

    private static string? GetRequiredPolicy(string path, string method)
    {
        if (path.StartsWith("node-pools", StringComparison.OrdinalIgnoreCase))
            return HttpMethods.IsGet(method) ? AuthPolicy.NodePoolOperate : AuthPolicy.NodePoolManage;

        if (path.StartsWith("runs", StringComparison.OrdinalIgnoreCase))
        {
            if (HttpMethods.IsGet(method)) return AuthPolicy.JobRead;
            if (HttpMethods.IsPost(method) && path.EndsWith("/cancel", StringComparison.OrdinalIgnoreCase))
                return AuthPolicy.JobOperate;
            return AuthPolicy.JobManage;
        }

        if (path.StartsWith("jobs", StringComparison.OrdinalIgnoreCase))
        {
            if (HttpMethods.IsGet(method)) return AuthPolicy.JobRead;
            if (HttpMethods.IsPost(method) && path.EndsWith("/trigger", StringComparison.OrdinalIgnoreCase))
                return AuthPolicy.JobOperate;
            return AuthPolicy.JobManage;
        }

        if (path.StartsWith("admin", StringComparison.OrdinalIgnoreCase))
        {
            // Timezones is public reference data needed by all authenticated users for scheduling.
            // The proxy endpoint requires authentication; no additional role restriction is needed.
            return path.EndsWith("/timezones", StringComparison.OrdinalIgnoreCase) ? null : AuthPolicy.Admin;
        }

        throw new InvalidOperationException(
            $"No authorization policy is defined for orchestrator path '{path}' ({method}). " +
            "Update GetRequiredPolicy to map this endpoint before it can be proxied.");
    }
}
