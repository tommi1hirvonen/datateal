using DuckHouse.Core.Mediator;
using DuckHouse.Orchestrator.Application.Mediator.Commands;

namespace DuckHouse.Orchestrator.Endpoints;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin").WithTags("Admin");

        group.MapPost("/purge-history", async (PurgeHistoryBody? body, IMediator mediator, CancellationToken ct) =>
        {
            var retentionDays = body?.RetentionDays ?? 30;
            var purged = await mediator.SendAsync(new PurgeHistoryRequest(retentionDays), ct);
            return Results.Ok(new { purged, retentionDays });
        })
        .WithName("PurgeHistory");

        group.MapGet("/timezones", () =>
        {
            var zones = TimeZoneInfo.GetSystemTimeZones()
                .Select(tz => new { id = tz.Id, displayName = tz.DisplayName });
            return Results.Ok(zones);
        })
        .WithName("GetTimeZones");

        return endpoints;
    }
}

public record PurgeHistoryBody(int RetentionDays = 30);
