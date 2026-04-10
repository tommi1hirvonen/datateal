using DuckHouse.Core.Mediator;
using DuckHouse.Orchestrator.Core.Entities;
using DuckHouse.Orchestrator.Core.Repositories;

namespace DuckHouse.Orchestrator.Application.Mediator.Commands;

public record CreateScheduleRequest(
    Guid JobId,
    string CronExpression,
    bool IsEnabled,
    string? TimeZone,
    Dictionary<string, string>? Parameters) : IRequest<JobSchedule>;

internal class CreateScheduleHandler(IScheduleRepository scheduleRepository)
    : IRequestHandler<CreateScheduleRequest, JobSchedule>
{
    public async Task<JobSchedule> Handle(CreateScheduleRequest request, CancellationToken cancellationToken)
    {
        var schedule = new JobSchedule
        {
            Id = Guid.NewGuid(),
            JobId = request.JobId,
            CronExpression = request.CronExpression,
            IsEnabled = request.IsEnabled,
            TimeZone = request.TimeZone,
            Parameters = request.Parameters,
        };

        return await scheduleRepository.CreateScheduleAsync(schedule, cancellationToken);
    }
}
