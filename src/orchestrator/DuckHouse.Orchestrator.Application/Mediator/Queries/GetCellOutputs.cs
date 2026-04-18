using System.Text.Json;
using DuckHouse.Core.Mediator;
using DuckHouse.Orchestrator.Core.Entities;
using DuckHouse.Orchestrator.Core.Repositories;

namespace DuckHouse.Orchestrator.Application.Mediator.Queries;

public record GetCellOutputsRequest(Guid TaskRunId) : IRequest<IReadOnlyList<CellOutputResult>>;

/// <summary>JSON shape returned to the client, matching CellOutputDto in DuckHouse.Ui.Shared.</summary>
public record CellOutputResult(
    Guid Id,
    int CellIndex,
    string CellSource,
    string CellType,
    string? Language,
    string Status,
    string? CellRole,
    string? OutputsJson,
    string? ErrorJson,
    int? ExecutionCount,
    double? DurationMs,
    DateTime? StartedAt,
    DateTime? CompletedAt);

internal class GetCellOutputsHandler(IJobRunRepository jobRunRepository)
    : IRequestHandler<GetCellOutputsRequest, IReadOnlyList<CellOutputResult>>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<CellOutputResult>> Handle(GetCellOutputsRequest request, CancellationToken cancellationToken)
    {
        var taskRun = await jobRunRepository.GetTaskRunAsync(request.TaskRunId, cancellationToken);
        if (taskRun?.OutputJson is null) return [];

        var notebook = JsonSerializer.Deserialize<RunNotebook>(taskRun.OutputJson, JsonOptions);
        if (notebook is null) return [];

        return notebook.Cells.Select(c => new CellOutputResult(
            Id: Guid.NewGuid(),
            CellIndex: c.Index,
            CellSource: c.Source,
            CellType: c.CellType,
            Language: c.Language == "Python" ? null : c.Language,
            Status: c.Status,
            CellRole: c.CellRole,
            OutputsJson: c.OutputsJson,
            ErrorJson: c.ErrorJson,
            ExecutionCount: c.ExecutionCount,
            DurationMs: c.DurationMs,
            StartedAt: c.StartedAt,
            CompletedAt: c.CompletedAt
        )).ToList();
    }
}
