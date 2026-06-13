using Datateal.Core.Mediator;
using Datateal.Orchestrator.Core.Entities;
using Datateal.Orchestrator.Core.Repositories;

namespace Datateal.Orchestrator.Application.Mediator.Queries;

public record GetSchedulesRequest(Guid WorkspaceId, Guid JobId) : IRequest<IReadOnlyList<JobSchedule>>;

internal class GetSchedulesHandler(IJobRepository jobRepository, IScheduleRepository scheduleRepository)
    : IRequestHandler<GetSchedulesRequest, IReadOnlyList<JobSchedule>>
{
    public async Task<IReadOnlyList<JobSchedule>> Handle(GetSchedulesRequest request, CancellationToken cancellationToken)
    {
        var job = await jobRepository.GetJobAsync(request.JobId, cancellationToken);
        if (job is null || job.WorkspaceId != request.WorkspaceId) return [];

        return await scheduleRepository.GetSchedulesForJobAsync(request.JobId, cancellationToken);
    }
}
