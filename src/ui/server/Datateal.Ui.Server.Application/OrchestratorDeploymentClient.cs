using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Datateal.Auth;
using Datateal.Core.Deployment;
using Datateal.Deployment.Diff;
using Datateal.Deployment.Models;
using Datateal.Deployment.Serialization;

namespace Datateal.Ui.Server.Application;

public static class OrchestratorDeploymentClient
{
    // The orchestrator serializes enums as strings; match that here for response deserialization.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };
    public static Task<ChangeSet> PlanJobsAsync(
        IHttpClientFactory httpClientFactory,
        Guid workspaceId,
        List<JobModel> jobs,
        string? actingUserId,
        CancellationToken ct) =>
        PostJobsAsync(httpClientFactory, workspaceId, "plan", jobs, actingUserId, ct);

    public static Task<ChangeSet> ApplyJobsAsync(
        IHttpClientFactory httpClientFactory,
        Guid workspaceId,
        List<JobModel> jobs,
        string? actingUserId,
        CancellationToken ct) =>
        PostJobsAsync(httpClientFactory, workspaceId, "apply", jobs, actingUserId, ct);

    public static async Task<List<JobModel>> ExportJobsAsync(
        IHttpClientFactory httpClientFactory,
        Guid workspaceId,
        CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient("Orchestrator");
            var jobs = await client.GetFromJsonAsync<List<JobListEntry>>($"/api/workspaces/{workspaceId}/jobs", ct) ?? [];
            var exported = new List<JobModel>(jobs.Count);

            foreach (var job in jobs.OrderBy(job => job.Name, StringComparer.OrdinalIgnoreCase))
            {
                using var response = await client.GetAsync($"/api/workspaces/{workspaceId}/jobs/{job.Id}/export", ct);
                var yaml = await ReadSuccessContentAsync(response, ct);
                exported.Add(BundleYaml.Deserialize<JobModel>(yaml));
            }

            return exported;
        }
        catch (Exception ex) when (IsOrchestratorConnectivityFailure(ex, ct))
        {
            throw ToOrchestratorUnavailableException(ex);
        }
    }

    private static async Task<ChangeSet> PostJobsAsync(
        IHttpClientFactory httpClientFactory,
        Guid workspaceId,
        string action,
        List<JobModel> jobs,
        string? actingUserId,
        CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient("Orchestrator");
            using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/workspaces/{workspaceId}/jobs/{action}")
            {
                Content = JsonContent.Create(jobs),
            };
            if (actingUserId is not null)
                request.Headers.TryAddWithoutValidation(DatatealHeaders.ActingUser, actingUserId);
            using var response = await client.SendAsync(request, ct);
            await EnsureSuccessAsync(response, ct);
            return (await response.Content.ReadFromJsonAsync<ChangeSet>(JsonOptions, ct))!;
        }
        catch (Exception ex) when (IsOrchestratorConnectivityFailure(ex, ct))
        {
            throw ToOrchestratorUnavailableException(ex);
        }
    }

    /// <summary>
    /// Distinguishes an actual orchestrator connectivity failure (connection refused, DNS
    /// failure, response timeout) from the caller's own <paramref name="ct"/> being cancelled —
    /// only the former should be translated into <see cref="DeploymentOrchestratorUnavailableException"/>;
    /// the latter must propagate as an ordinary <see cref="OperationCanceledException"/> so normal
    /// request-cancellation handling still applies.
    /// </summary>
    private static bool IsOrchestratorConnectivityFailure(Exception ex, CancellationToken ct) =>
        ex is HttpRequestException
        || (ex is TaskCanceledException && !ct.IsCancellationRequested);

    private static DeploymentOrchestratorUnavailableException ToOrchestratorUnavailableException(Exception ex) =>
        new("The job orchestrator is unavailable or did not respond in time. No changes were made.", ex);

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
            return;

        var content = await response.Content.ReadAsStringAsync(ct);
        throw new InvalidOperationException(
            $"Orchestrator deployment request failed with status {(int)response.StatusCode}: {content}");
    }

    private static async Task<string> ReadSuccessContentAsync(HttpResponseMessage response, CancellationToken ct)
    {
        await EnsureSuccessAsync(response, ct);
        return await response.Content.ReadAsStringAsync(ct);
    }

    private sealed record JobListEntry(Guid Id, string Name);
}
