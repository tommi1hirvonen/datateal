using DuckHouse.Core.Mediator;
using DuckHouse.Orchestrator.Application.Mediator.Commands;
using DuckHouse.Orchestrator.Application.Mediator.Queries;

namespace DuckHouse.Orchestrator.Endpoints;

public static class JobEndpoints
{
    public static IEndpointRouteBuilder MapJobEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/jobs").WithTags("Jobs");

        group.MapGet("/", async (IMediator mediator, CancellationToken ct) =>
            Results.Ok(await mediator.SendAsync(new GetJobsRequest(), ct)))
            .WithName("ListJobs");

        group.MapGet("/{id:guid}", async (Guid id, IMediator mediator, CancellationToken ct) =>
        {
            var job = await mediator.SendAsync(new GetJobRequest(id), ct);
            return job is null ? Results.NotFound() : Results.Ok(job);
        })
        .WithName("GetJob");

        group.MapPost("/", async (CreateJobRequest request, IMediator mediator, CancellationToken ct) =>
        {
            var job = await mediator.SendAsync(request, ct);
            return Results.Created($"/api/jobs/{job.Id}", job);
        })
        .WithName("CreateJob");

        group.MapPut("/{id:guid}", async (Guid id, UpdateJobRequest request, IMediator mediator, CancellationToken ct) =>
        {
            var updated = request with { Id = id };
            var job = await mediator.SendAsync(updated, ct);
            return job is null ? Results.NotFound() : Results.Ok(job);
        })
        .WithName("UpdateJob");

        group.MapDelete("/{id:guid}", async (Guid id, IMediator mediator, CancellationToken ct) =>
        {
            await mediator.SendAsync(new DeleteJobRequest(id), ct);
            return Results.NoContent();
        })
        .WithName("DeleteJob");

        group.MapPost("/{id:guid}/trigger", async (Guid id, TriggerJobBody? body, IMediator mediator, CancellationToken ct) =>
        {
            var run = await mediator.SendAsync(new TriggerJobRequest(id, body?.Parameters), ct);
            return Results.Accepted($"/api/runs/{run.Id}", run);
        })
        .WithName("TriggerJob");

        group.MapGet("/{id:guid}/runs", async (Guid id, int? limit, int? offset, IMediator mediator, CancellationToken ct) =>
            Results.Ok(await mediator.SendAsync(new GetJobRunsRequest(id, limit ?? 20, offset ?? 0), ct)))
            .WithName("GetJobRuns");

        group.MapGet("/{id:guid}/schedules", async (Guid id, IMediator mediator, CancellationToken ct) =>
            Results.Ok(await mediator.SendAsync(new GetSchedulesRequest(id), ct)))
            .WithName("GetSchedules");

        group.MapPost("/{id:guid}/schedules", async (Guid id, CreateScheduleRequest request, IMediator mediator, CancellationToken ct) =>
        {
            var withJobId = request with { JobId = id };
            var schedule = await mediator.SendAsync(withJobId, ct);
            return Results.Created($"/api/jobs/{id}/schedules/{schedule.Id}", schedule);
        })
        .WithName("CreateSchedule");

        group.MapPut("/{id:guid}/schedules/{sid:guid}", async (Guid id, Guid sid, UpdateScheduleRequest request, IMediator mediator, CancellationToken ct) =>
        {
            var updated = request with { Id = sid };
            var schedule = await mediator.SendAsync(updated, ct);
            return schedule is null ? Results.NotFound() : Results.Ok(schedule);
        })
        .WithName("UpdateSchedule");

        group.MapDelete("/{id:guid}/schedules/{sid:guid}", async (Guid id, Guid sid, IMediator mediator, CancellationToken ct) =>
        {
            await mediator.SendAsync(new DeleteScheduleRequest(sid), ct);
            return Results.NoContent();
        })
        .WithName("DeleteSchedule");

        return endpoints;
    }
}

public record TriggerJobBody(Dictionary<string, string>? Parameters);
