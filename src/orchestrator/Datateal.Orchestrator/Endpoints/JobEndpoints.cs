using Datateal.Core.Mediator;
using Datateal.Orchestrator.Application.Mediator.Commands;
using Datateal.Orchestrator.Application.Mediator.Queries;
using Datateal.Orchestrator.Core.Entities;

namespace Datateal.Orchestrator.Endpoints;

public static class JobEndpoints
{
    public static IEndpointRouteBuilder MapJobEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/workspaces/{workspaceId:guid}/jobs").WithTags("Jobs");

        group.MapGet("/", async (Guid workspaceId, IMediator mediator, CancellationToken ct) =>
            Results.Ok(await mediator.SendAsync(new GetJobsRequest(workspaceId), ct)))
            .WithName("ListJobs");

        group.MapGet("/{id:guid}", async (Guid workspaceId, Guid id, IMediator mediator, CancellationToken ct) =>
        {
            var job = await mediator.SendAsync(new GetJobRequest(id), ct);
            return job is null ? Results.NotFound() : Results.Ok(job);
        })
        .WithName("GetJob");

        group.MapPost("/", async (Guid workspaceId, CreateJobRequest request, IMediator mediator, CancellationToken ct) =>
        {
            try
            {
                var withWorkspace = request with { WorkspaceId = workspaceId };
                var job = await mediator.SendAsync(withWorkspace, ct);
                return Results.Created($"/api/workspaces/{workspaceId}/jobs/{job.Id}", job);
            }
            catch (JobNameConflictException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("CreateJob");

        group.MapPut("/{id:guid}", async (Guid workspaceId, Guid id, UpdateJobRequest request, IMediator mediator, CancellationToken ct) =>
        {
            try
            {
                var updated = request with { Id = id };
                var job = await mediator.SendAsync(updated, ct);
                return job is null ? Results.NotFound() : Results.Ok(job);
            }
            catch (JobNameConflictException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("UpdateJob");

        group.MapDelete("/{id:guid}", async (Guid workspaceId, Guid id, IMediator mediator, CancellationToken ct) =>
        {
            await mediator.SendAsync(new DeleteJobRequest(id), ct);
            return Results.NoContent();
        })
        .WithName("DeleteJob");

        group.MapPost("/{id:guid}/trigger", async (Guid workspaceId, Guid id, TriggerJobBody? body, IMediator mediator, CancellationToken ct) =>
        {
            try
            {
                var run = await mediator.SendAsync(new TriggerJobRequest(id, body?.Parameters), ct);
                return Results.Accepted($"/api/workspaces/{workspaceId}/runs/{run.Id}", run);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("TriggerJob");

        group.MapGet("/{id:guid}/runs", async (Guid workspaceId, Guid id, int? limit, int? offset, IMediator mediator, CancellationToken ct) =>
            Results.Ok(await mediator.SendAsync(new GetJobRunsRequest(id, limit ?? 20, offset ?? 0), ct)))
            .WithName("GetJobRuns");

        group.MapGet("/{id:guid}/schedules", async (Guid workspaceId, Guid id, IMediator mediator, CancellationToken ct) =>
            Results.Ok(await mediator.SendAsync(new GetSchedulesRequest(id), ct)))
            .WithName("GetSchedules");

        group.MapPost("/{id:guid}/schedules", async (Guid workspaceId, Guid id, CreateScheduleRequest request, IMediator mediator, CancellationToken ct) =>
        {
            var withJobId = request with { JobId = id };
            var schedule = await mediator.SendAsync(withJobId, ct);
            return Results.Created($"/api/workspaces/{workspaceId}/jobs/{id}/schedules/{schedule.Id}", schedule);
        })
        .WithName("CreateSchedule");

        group.MapPut("/{id:guid}/schedules/{sid:guid}", async (Guid workspaceId, Guid id, Guid sid, UpdateScheduleRequest request, IMediator mediator, CancellationToken ct) =>
        {
            var updated = request with { Id = sid };
            var schedule = await mediator.SendAsync(updated, ct);
            return schedule is null ? Results.NotFound() : Results.Ok(schedule);
        })
        .WithName("UpdateSchedule");

        group.MapDelete("/{id:guid}/schedules/{sid:guid}", async (Guid workspaceId, Guid id, Guid sid, IMediator mediator, CancellationToken ct) =>
        {
            await mediator.SendAsync(new DeleteScheduleRequest(sid), ct);
            return Results.NoContent();
        })
        .WithName("DeleteSchedule");

        // Import job from YAML
        group.MapPost("/import", async (Guid workspaceId, ImportJobBody body, IMediator mediator, CancellationToken ct) =>
        {
            try
            {
                var job = await mediator.SendAsync(new ImportJobRequest(workspaceId, body.Yaml), ct);
                return Results.Created($"/api/workspaces/{workspaceId}/jobs/{job.Id}", job);
            }
            catch (JobNameConflictException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .WithName("ImportJob");

        // Export job as YAML
        group.MapGet("/{id:guid}/export", async (Guid workspaceId, Guid id, IMediator mediator, CancellationToken ct) =>
        {
            var yaml = await mediator.SendAsync(new ExportJobRequest(id), ct);
            return yaml is null ? Results.NotFound() : Results.Content(yaml, "application/yaml");
        })
        .WithName("ExportJob");

        return endpoints;
    }
}

public record TriggerJobBody(Dictionary<string, string>? Parameters);
public record ImportJobBody(string Yaml);
