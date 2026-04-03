using DuckHouse.ControlPlane.Api.Nodes.Kernels;

namespace DuckHouse.ControlPlane.Api.Nodes;

public static class NodeEndpoints
{
    public static IEndpointRouteBuilder MapNodeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/nodes").WithTags("Nodes");

        group.MapGet("/", async (INodeService nodeService, CancellationToken ct) =>
            Results.Ok(await nodeService.ListNodesAsync(ct)))
            .WithName("ListNodes");

        group.MapPost("/", async (CreateNodeRequest request, INodeService nodeService, CancellationToken ct) =>
        {
            var node = await nodeService.CreateNodeAsync(request, ct);
            return Results.Created($"/nodes/{node.Name}", node);
        })
        .WithName("CreateNode");

        group.MapDelete("/{name}", async (string name, INodeService nodeService, CancellationToken ct) =>
        {
            await nodeService.RemoveNodeAsync(name, ct);
            return Results.NoContent();
        })
        .WithName("RemoveNode");

        group.MapPost("/{name}/stop", async (string name, INodeService nodeService, CancellationToken ct) =>
        {
            await nodeService.StopNodeAsync(name, ct);
            return Results.NoContent();
        })
        .WithName("StopNode");

        group.MapPost("/{name}/start", async (string name, INodeService nodeService, CancellationToken ct) =>
        {
            await nodeService.StartNodeAsync(name, ct);
            return Results.NoContent();
        })
        .WithName("StartNode");

        // ── Kernel proxy endpoints ────────────────────────────────────────────
        // These forward to the duckhouse-runtime FastAPI running on the pod via
        // the Kubernetes API server HTTP proxy (no Service or public IP required).

        var kernels = group.MapGroup("/{name}/kernels").WithTags("Kernels");

        kernels.MapGet("/", async (string name, INodeRuntimeClient runtime, CancellationToken ct) =>
            Results.Ok(await runtime.ListKernelsAsync(name, ct)))
            .WithName("ListKernels");

        kernels.MapPost("/", async (string name, INodeRuntimeClient runtime, CancellationToken ct) =>
        {
            var kernel = await runtime.CreateKernelAsync(name, ct);
            return Results.Created($"/nodes/{name}/kernels/{kernel.Id}", kernel);
        })
        .WithName("CreateKernel");

        kernels.MapGet("/{kernelId}", async (string name, string kernelId, INodeRuntimeClient runtime, CancellationToken ct) =>
            Results.Ok(await runtime.GetKernelAsync(name, kernelId, ct)))
            .WithName("GetKernel");

        kernels.MapDelete("/{kernelId}", async (string name, string kernelId, INodeRuntimeClient runtime, CancellationToken ct) =>
        {
            await runtime.DeleteKernelAsync(name, kernelId, ct);
            return Results.NoContent();
        })
        .WithName("DeleteKernel");

        kernels.MapPost("/{kernelId}/execute", async (string name, string kernelId, ExecuteRequest request, INodeRuntimeClient runtime, CancellationToken ct) =>
            Results.Ok(await runtime.ExecuteAsync(name, kernelId, request, ct)))
            .WithName("ExecuteKernel");

        kernels.MapPost("/{kernelId}/restart", async (string name, string kernelId, INodeRuntimeClient runtime, CancellationToken ct) =>
            Results.Ok(await runtime.RestartKernelAsync(name, kernelId, ct)))
            .WithName("RestartKernel");

        kernels.MapPost("/{kernelId}/interrupt", async (string name, string kernelId, INodeRuntimeClient runtime, CancellationToken ct) =>
        {
            await runtime.InterruptKernelAsync(name, kernelId, ct);
            return Results.NoContent();
        })
        .WithName("InterruptKernel");

        return endpoints;
    }
}
