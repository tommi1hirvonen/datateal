using Datateal.Core.Mediator;
using Datateal.Orchestrator.Application.Mediator.Commands;
using Datateal.Orchestrator.Application.Mediator.Queries;

namespace Datateal.Orchestrator.Endpoints;

public static class RunEndpoints
{
    public static IEndpointRouteBuilder MapRunEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/workspaces/{workspaceId:guid}/runs").WithTags("Runs");

        group.MapGet("/", async (Guid workspaceId, string? jobName, string? status, DateTime? from, DateTime? to, int? limit, int? offset, IMediator mediator, CancellationToken ct) =>
            Results.Ok(await mediator.SendAsync(
                new GetAllRunsRequest(workspaceId, jobName, status, from, to, limit ?? 100, offset ?? 0), ct)))
            .WithName("GetAllRuns");

        group.MapGet("/{id:guid}", async (Guid workspaceId, Guid id, IMediator mediator, CancellationToken ct) =>
        {
            var run = await mediator.SendAsync(new GetJobRunRequest(workspaceId, id), ct);
            return run is null ? Results.NotFound() : Results.Ok(run);
        })
        .WithName("GetJobRun");

        group.MapGet("/{id:guid}/tasks/{taskRunId:guid}", async (Guid workspaceId, Guid id, Guid taskRunId, IMediator mediator, CancellationToken ct) =>
        {
            var run = await mediator.SendAsync(new GetJobRunRequest(workspaceId, id), ct);
            if (run is null) return Results.NotFound();

            var taskRun = await mediator.SendAsync(new GetTaskRunRequest(taskRunId), ct);
            return taskRun is null || taskRun.JobRunId != id ? Results.NotFound() : Results.Ok(taskRun);
        })
        .WithName("GetTaskRun");

        group.MapGet("/{id:guid}/tasks/{taskRunId:guid}/cells", async (Guid workspaceId, Guid id, Guid taskRunId, IMediator mediator, CancellationToken ct) =>
        {
            var run = await mediator.SendAsync(new GetJobRunRequest(workspaceId, id), ct);
            if (run is null) return Results.NotFound();

            var taskRun = await mediator.SendAsync(new GetTaskRunRequest(taskRunId), ct);
            if (taskRun is null || taskRun.JobRunId != id) return Results.NotFound();

            return Results.Ok(await mediator.SendAsync(new GetCellOutputsRequest(taskRunId), ct));
        })
        .WithName("GetCellOutputs");

        group.MapPost("/{id:guid}/cancel", async (Guid workspaceId, Guid id, IMediator mediator, CancellationToken ct) =>
        {
            try
            {
                await mediator.SendAsync(new CancelRunRequest(workspaceId, id), ct);
                return Results.NoContent();
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound();
            }
        })
        .WithName("CancelRun");

        return endpoints;
    }
}
