using DuckHouse.Core.Mediator;
using DuckHouse.Orchestrator.Core.Entities;
using DuckHouse.Orchestrator.Core.Repositories;

namespace DuckHouse.Orchestrator.Application.Mediator.Queries;

public record GetSchedulesRequest(Guid JobId) : IRequest<IReadOnlyList<JobSchedule>>;

internal class GetSchedulesHandler(IScheduleRepository scheduleRepository)
    : IRequestHandler<GetSchedulesRequest, IReadOnlyList<JobSchedule>>
{
    public async Task<IReadOnlyList<JobSchedule>> Handle(GetSchedulesRequest request, CancellationToken cancellationToken)
    {
        return await scheduleRepository.GetSchedulesForJobAsync(request.JobId, cancellationToken);
    }
}
