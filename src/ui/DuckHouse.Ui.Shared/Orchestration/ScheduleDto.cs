namespace DuckHouse.Ui.Shared.Orchestration;

public record ScheduleDto(Guid Id, string CronExpression, bool IsEnabled, string? TimeZone, Dictionary<string, string>? Parameters, DateTimeOffset? NextFireTime);
