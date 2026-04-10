using DuckHouse.Core.Mediator;
using DuckHouse.Orchestrator.Application.Mediator.Commands;
using DuckHouse.Orchestrator.Application.Mediator.Queries;

namespace DuckHouse.Orchestrator.Endpoints;

public static class NodePoolEndpoints
{
    public static IEndpointRouteBuilder MapNodePoolEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/node-pools").WithTags("Node Pools");

        group.MapGet("/", async (IMediator mediator, CancellationToken ct) =>
            Results.Ok(await mediator.SendAsync(new GetNodePoolConfigsRequest(), ct)))
            .WithName("ListNodePoolConfigs");

        group.MapGet("/{id:guid}", async (Guid id, IMediator mediator, CancellationToken ct) =>
        {
            var config = await mediator.SendAsync(new GetNodePoolConfigRequest(id), ct);
            return config is null ? Results.NotFound() : Results.Ok(config);
        })
        .WithName("GetNodePoolConfig");

        group.MapPost("/", async (CreateNodePoolConfigRequest request, IMediator mediator, CancellationToken ct) =>
        {
            var config = await mediator.SendAsync(request, ct);
            return Results.Created($"/api/node-pools/{config.Id}", config);
        })
        .WithName("CreateNodePoolConfig");

        group.MapPut("/{id:guid}", async (Guid id, UpdateNodePoolConfigRequest request, IMediator mediator, CancellationToken ct) =>
        {
            var updated = request with { Id = id };
            var config = await mediator.SendAsync(updated, ct);
            return config is null ? Results.NotFound() : Results.Ok(config);
        })
        .WithName("UpdateNodePoolConfig");

        group.MapDelete("/{id:guid}", async (Guid id, IMediator mediator, CancellationToken ct) =>
        {
            await mediator.SendAsync(new DeleteNodePoolConfigRequest(id), ct);
            return Results.NoContent();
        })
        .WithName("DeleteNodePoolConfig");

        return endpoints;
    }
}
