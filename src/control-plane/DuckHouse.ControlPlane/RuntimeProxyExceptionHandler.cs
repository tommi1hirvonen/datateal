using Azure;
using k8s.Autorest;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;

namespace DuckHouse.ControlPlane;

// Propagates HTTP status codes from the duckhouse-runtime FastAPI (via the Kubernetes
// API server proxy) back to the caller as proper HTTP responses instead of 500s.
// For example, when the runtime returns 404 (kernel evicted), the control plane
// returns 404 rather than letting the unhandled HttpRequestException become a 500.
// Also handles Azure SDK and Kubernetes management exceptions so unhandled ARM/K8s
// errors produce a meaningful status code rather than a generic 500.
internal sealed class RuntimeProxyExceptionHandler(
    ILogger<RuntimeProxyExceptionHandler> logger,
    IHostEnvironment environment) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        int? statusCode = exception switch
        {
            HttpRequestException { StatusCode: { } s } => (int)s,
            RequestFailedException e => e.Status,
            HttpOperationException e => (int)e.Response.StatusCode,
            _ => null,
        };

        if (statusCode is null)
            return false;

        logger.LogWarning(
            "Upstream service returned {StatusCode}: {Message}",
            statusCode, exception.Message);

        var title = environment.IsDevelopment()
            ? exception.Message
            : "An upstream service error occurred.";

        httpContext.Response.StatusCode = statusCode.Value;
        await httpContext.Response.WriteAsJsonAsync(
            new ProblemDetails { Status = statusCode, Title = title },
            cancellationToken: cancellationToken);
        return true;
    }
}
