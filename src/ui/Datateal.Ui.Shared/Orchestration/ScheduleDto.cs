namespace Datateal.Ui.Shared.Orchestration;

public record ScheduleDto(Guid Id, string Name, string CronExpression, bool IsEnabled, string? TimeZone, Dictionary<string, string>? Parameters, DateTimeOffset? NextFireTime);
