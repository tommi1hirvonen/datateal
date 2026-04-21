using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DuckHouse.Core.Orchestration;
using DuckHouse.Ui.Shared.Orchestration;

namespace DuckHouse.Ui.Client.Services;

internal class JobService(HttpClient httpClient) : IJobService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<IReadOnlyList<JobSummary>> GetJobsAsync(CancellationToken ct)
    {
        return await httpClient.GetFromJsonAsync<IReadOnlyList<JobSummary>>("api/orchestrator/jobs", JsonOptions, ct)
            ?? [];
    }

    public async Task<JobDetail?> GetJobAsync(Guid id, CancellationToken ct)
    {
        var response = await httpClient.GetAsync($"api/orchestrator/jobs/{id}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JobDetail>(JsonOptions, ct);
    }

    public async Task<JobSummary> CreateJobAsync(CreateJobRequest request, CancellationToken ct)
    {
        var response = await httpClient.PostAsJsonAsync("api/orchestrator/jobs", request, JsonOptions, ct);
        await EnsureNoJobErrorAsync(response, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JobSummary>(JsonOptions, ct))!;
    }

    public async Task<JobSummary?> UpdateJobAsync(Guid id, UpdateJobRequest request, CancellationToken ct)
    {
        var response = await httpClient.PutAsJsonAsync($"api/orchestrator/jobs/{id}", request, JsonOptions, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        await EnsureNoJobErrorAsync(response, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JobSummary>(JsonOptions, ct);
    }

    public async Task DeleteJobAsync(Guid id, CancellationToken ct)
    {
        var response = await httpClient.DeleteAsync($"api/orchestrator/jobs/{id}", ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<JobRunSummary> TriggerJobAsync(Guid id, TriggerJobRequest? request, CancellationToken ct)
    {
        var response = await httpClient.PostAsJsonAsync($"api/orchestrator/jobs/{id}/trigger",
            request ?? new TriggerJobRequest(), JsonOptions, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JobRunSummary>(JsonOptions, ct))!;
    }

    public async Task<IReadOnlyList<JobRunSummary>> GetRunsAsync(Guid jobId, int limit, CancellationToken ct)
    {
        return await httpClient.GetFromJsonAsync<IReadOnlyList<JobRunSummary>>(
            $"api/orchestrator/jobs/{jobId}/runs?limit={limit}", JsonOptions, ct) ?? [];
    }

    public async Task<IReadOnlyList<JobRunSummary>> GetAllRunsAsync(string? jobName, JobRunStatus? status, DateTime? from, DateTime? to, int limit, CancellationToken ct)
    {
        var parts = new List<string> { $"limit={limit}" };
        if (!string.IsNullOrWhiteSpace(jobName)) parts.Add($"jobName={Uri.EscapeDataString(jobName)}");
        if (status.HasValue) parts.Add($"status={Uri.EscapeDataString(status.Value.ToString())}");
        if (from.HasValue) parts.Add($"from={Uri.EscapeDataString(from.Value.ToString("o"))}");
        if (to.HasValue) parts.Add($"to={Uri.EscapeDataString(to.Value.ToString("o"))}");
        return await httpClient.GetFromJsonAsync<IReadOnlyList<JobRunSummary>>(
            $"api/orchestrator/runs?{string.Join("&", parts)}", JsonOptions, ct) ?? [];
    }

    public async Task<JobRunDetail?> GetRunAsync(Guid runId, CancellationToken ct)
    {
        var response = await httpClient.GetAsync($"api/orchestrator/runs/{runId}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JobRunDetail>(JsonOptions, ct);
    }

    public async Task CancelRunAsync(Guid runId, CancellationToken ct)
    {
        var response = await httpClient.PostAsync($"api/orchestrator/runs/{runId}/cancel", null, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<CellOutputDto>> GetCellOutputsAsync(Guid runId, Guid taskRunId, CancellationToken ct)
    {
        return await httpClient.GetFromJsonAsync<IReadOnlyList<CellOutputDto>>(
            $"api/orchestrator/runs/{runId}/tasks/{taskRunId}/cells", JsonOptions, ct) ?? [];
    }

    public async Task<IReadOnlyList<ScheduleDto>> GetSchedulesAsync(Guid jobId, CancellationToken ct)
    {
        return await httpClient.GetFromJsonAsync<IReadOnlyList<ScheduleDto>>(
            $"api/orchestrator/jobs/{jobId}/schedules", JsonOptions, ct) ?? [];
    }

    public async Task<ScheduleDto> CreateScheduleAsync(Guid jobId, CreateScheduleRequest request, CancellationToken ct)
    {
        var response = await httpClient.PostAsJsonAsync($"api/orchestrator/jobs/{jobId}/schedules", request, JsonOptions, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ScheduleDto>(JsonOptions, ct))!;
    }

    public async Task<ScheduleDto?> UpdateScheduleAsync(Guid jobId, Guid scheduleId, UpdateScheduleRequest request, CancellationToken ct)
    {
        var response = await httpClient.PutAsJsonAsync($"api/orchestrator/jobs/{jobId}/schedules/{scheduleId}", request, JsonOptions, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ScheduleDto>(JsonOptions, ct);
    }

    public async Task DeleteScheduleAsync(Guid jobId, Guid scheduleId, CancellationToken ct)
    {
        var response = await httpClient.DeleteAsync($"api/orchestrator/jobs/{jobId}/schedules/{scheduleId}", ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<TimeZoneDto>> GetTimeZonesAsync(CancellationToken ct)
    {
        return await httpClient.GetFromJsonAsync<IReadOnlyList<TimeZoneDto>>(
            "api/orchestrator/admin/timezones", JsonOptions, ct) ?? [];
    }

    public async Task<string> ExportJobAsync(Guid id, CancellationToken ct)
    {
        var response = await httpClient.GetAsync($"api/orchestrator/jobs/{id}/export", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }

    public async Task<JobSummary> ImportJobAsync(string yaml, CancellationToken ct)
    {
        var response = await httpClient.PostAsJsonAsync("api/orchestrator/jobs/import", new { yaml }, JsonOptions, ct);
        await EnsureNoJobErrorAsync(response, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JobSummary>(JsonOptions, ct))!;
    }

    /// <summary>
    /// Reads the <c>{ "error": "..." }</c> body from 400 Bad Request and 409 Conflict responses
    /// and throws <see cref="InvalidOperationException"/> with that message so callers display it correctly.
    /// </summary>
    private static async Task EnsureNoJobErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.StatusCode is not (HttpStatusCode.BadRequest or HttpStatusCode.Conflict)) return;

        string? message = null;
        try
        {
            var body = await response.Content.ReadFromJsonAsync<JobErrorBody>(JsonOptions, ct);
            message = body?.Error;
        }
        catch { /* fall through to default message */ }

        throw new InvalidOperationException(message ?? "The operation failed. Please check the job configuration.");
    }

    private sealed record JobErrorBody(string? Error);
}
