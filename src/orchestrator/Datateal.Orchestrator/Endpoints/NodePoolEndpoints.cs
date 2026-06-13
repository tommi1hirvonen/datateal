using Datateal.Core.Mediator;
using Datateal.Orchestrator.Application.Mediator.Commands;
using Datateal.Orchestrator.Application.Mediator.Queries;

namespace Datateal.Orchestrator.Endpoints;

public static class NodePoolEndpoints
{
    public static IEndpointRouteBuilder MapNodePoolEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/workspaces/{workspaceId:guid}/node-pools").WithTags("Node Pools");

        group.MapGet("/", async (Guid workspaceId, IMediator mediator, CancellationToken ct) =>
            Results.Ok(await mediator.SendAsync(new GetNodePoolConfigsRequest(workspaceId), ct)))
            .WithName("ListNodePoolConfigs");

        group.MapGet("/{id:guid}", async (Guid workspaceId, Guid id, IMediator mediator, CancellationToken ct) =>
        {
            var config = await mediator.SendAsync(new GetNodePoolConfigRequest(id), ct);
            return config is null ? Results.NotFound() : Results.Ok(config);
        })
        .WithName("GetNodePoolConfig");

        group.MapPost("/", async (Guid workspaceId, CreateNodePoolConfigRequest request, IMediator mediator, CancellationToken ct) =>
        {
            var withWorkspace = request with { WorkspaceId = workspaceId };
            var config = await mediator.SendAsync(withWorkspace, ct);
            return Results.Created($"/api/workspaces/{workspaceId}/node-pools/{config.Id}", config);
        })
        .WithName("CreateNodePoolConfig");

        group.MapPut("/{id:guid}", async (Guid workspaceId, Guid id, UpdateNodePoolConfigRequest request, IMediator mediator, CancellationToken ct) =>
        {
            var updated = request with { Id = id };
            var config = await mediator.SendAsync(updated, ct);
            return config is null ? Results.NotFound() : Results.Ok(config);
        })
        .WithName("UpdateNodePoolConfig");

        group.MapDelete("/{id:guid}", async (Guid workspaceId, Guid id, IMediator mediator, CancellationToken ct) =>
        {
            await mediator.SendAsync(new DeleteNodePoolConfigRequest(id), ct);
            return Results.NoContent();
        })
        .WithName("DeleteNodePoolConfig");

        return endpoints;
    }
}
