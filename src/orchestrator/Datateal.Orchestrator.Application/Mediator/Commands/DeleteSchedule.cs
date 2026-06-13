using Datateal.Core.Mediator;
using Datateal.Orchestrator.Application.Engine;
using Datateal.Orchestrator.Core.Repositories;

namespace Datateal.Orchestrator.Application.Mediator.Commands;

public record DeleteScheduleRequest(Guid WorkspaceId, Guid Id) : IRequest<bool>;

internal class DeleteScheduleHandler(
    IJobRepository jobRepository,
    IScheduleRepository scheduleRepository,
    SchedulesManager schedulesManager)
    : IRequestHandler<DeleteScheduleRequest, bool>
{
    public async Task<bool> Handle(DeleteScheduleRequest request, CancellationToken cancellationToken)
    {
        var schedule = await scheduleRepository.GetScheduleAsync(request.Id, cancellationToken);
        if (schedule is null) return false;

        var job = await jobRepository.GetJobAsync(schedule.JobId, cancellationToken);
        if (job is null || job.WorkspaceId != request.WorkspaceId) return false;

        var deleted = await scheduleRepository.DeleteScheduleAsync(request.Id, cancellationToken);
        if (!deleted) return false;

        await schedulesManager.RemoveScheduleAsync(schedule, cancellationToken);
        return true;
    }
}
