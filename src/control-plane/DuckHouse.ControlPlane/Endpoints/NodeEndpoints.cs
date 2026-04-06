using DuckHouse.ControlPlane.Application.Mediator.Commands;
using DuckHouse.ControlPlane.Application.Mediator.Queries;
using DuckHouse.Core.Kernels;
using DuckHouse.Core.Mediator;
namespace DuckHouse.ControlPlane.Endpoints;

public static class NodeEndpoints
{
    public static IEndpointRouteBuilder MapNodeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/nodes").WithTags("Nodes");

        group.MapGet("/", async (IMediator mediator, CancellationToken ct) =>
            Results.Ok(await mediator.SendAsync(new GetNodesRequest(), ct)))
            .WithName("ListNodes");

        group.MapGet("/{name}", async (string name, IMediator mediator, CancellationToken ct) =>
        {
            var node = await mediator.SendAsync(new GetNodeRequest(name), ct);
            return node is null ? Results.NotFound() : Results.Ok(node);
        })
        .WithName("GetNode");

        group.MapPost("/", async (CreateNodeRequest request, IMediator mediator, CancellationToken ct) =>
        {
            var node = await mediator.SendAsync(request, ct);
            return Results.Created($"/nodes/{node.Name}", node);
        })
        .WithName("CreateNode");

        group.MapDelete("/{name}", async (string name, IMediator mediator, CancellationToken ct) =>
        {
            await mediator.SendAsync(new RemoveNodeRequest(name), ct);
            return Results.NoContent();
        })
        .WithName("RemoveNode");

        group.MapPost("/{name}/stop", async (string name, IMediator mediator, CancellationToken ct) =>
        {
            await mediator.SendAsync(new StopNodeRequest(name), ct);
            return Results.NoContent();
        })
        .WithName("StopNode");

        group.MapPost("/{name}/start", async (string name, IMediator mediator, CancellationToken ct) =>
        {
            await mediator.SendAsync(new StartNodeRequest(name), ct);
            return Results.NoContent();
        })
        .WithName("StartNode");

        // ── Kernel proxy endpoints ────────────────────────────────────────────
        // These forward to the duckhouse-runtime FastAPI running on the pod via
        // the Kubernetes API server HTTP proxy (no Service or public IP required).

        var kernels = group.MapGroup("/{name}/kernels").WithTags("Kernels");

        kernels.MapGet("/", async (string name, IMediator mediator, CancellationToken ct) =>
            Results.Ok(await mediator.SendAsync(new GetKernelsRequest(name), ct)))
            .WithName("ListKernels");

        kernels.MapPost("/", async (string name, IMediator mediator, CancellationToken ct) =>
        {
            var kernel = await mediator.SendAsync(new CreateKernelRequest(name), ct);
            return Results.Created($"/nodes/{name}/kernels/{kernel.Id}", kernel);
        })
        .WithName("CreateKernel");

        kernels.MapGet("/{kernelId}", async (string name, string kernelId, IMediator mediator, CancellationToken ct) =>
            Results.Ok(await mediator.SendAsync(new GetKernelRequest(name, kernelId), ct)))
            .WithName("GetKernel");

        kernels.MapDelete("/{kernelId}", async (string name, string kernelId, IMediator mediator, CancellationToken ct) =>
        {
            await mediator.SendAsync(new DeleteKernelRequest(name, kernelId), ct);
            return Results.NoContent();
        })
        .WithName("DeleteKernel");

        kernels.MapPost("/{kernelId}/execute", async (string name, string kernelId, ExecuteRequest request, IMediator mediator, CancellationToken ct) =>
        {
            var handle = await mediator.SendAsync(new ExecuteKernelRequest(name, kernelId, request.Code, request.Timeout), ct);
            return Results.Accepted(value: handle);
        })
        .WithName("ExecuteKernel");

        kernels.MapGet("/{kernelId}/executions/{executionId}", async (string name, string kernelId, string executionId, IMediator mediator, CancellationToken ct) =>
            Results.Ok(await mediator.SendAsync(new PollExecutionRequest(name, kernelId, executionId), ct)))
            .WithName("PollExecution");

        kernels.MapPost("/{kernelId}/restart", async (string name, string kernelId, IMediator mediator, CancellationToken ct) =>
            Results.Ok(await mediator.SendAsync(new RestartKernelRequest(name, kernelId), ct)))
            .WithName("RestartKernel");

        kernels.MapPost("/{kernelId}/interrupt", async (string name, string kernelId, IMediator mediator, CancellationToken ct) =>
        {
            await mediator.SendAsync(new InterruptKernelRequest(name, kernelId), ct);
            return Results.NoContent();
        })
        .WithName("InterruptKernel");

        kernels.MapPost("/{kernelId}/completions", async (string name, string kernelId, CompleteRequest request, IMediator mediator, CancellationToken ct) =>
            Results.Ok(await mediator.SendAsync(new CompleteKernelRequest(name, kernelId, request), ct)))
            .WithName("CompleteKernel");

        kernels.MapPost("/{kernelId}/diagnostics", async (string name, string kernelId, DiagnoseRequest request, IMediator mediator, CancellationToken ct) =>
            Results.Ok(await mediator.SendAsync(new DiagnoseKernelRequest(name, kernelId, request), ct)))
            .WithName("DiagnoseKernel");

        return endpoints;
    }
}
