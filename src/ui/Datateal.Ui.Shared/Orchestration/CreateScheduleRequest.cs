namespace Datateal.Ui.Shared.Orchestration;

public record CreateScheduleRequest(string Name, string CronExpression, bool IsEnabled, string? TimeZone, Dictionary<string, string>? Parameters);
