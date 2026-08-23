using Datateal.Core.Deployment;
using Datateal.Deployment.Models;
using Datateal.Ui.Server.Application;
using Xunit;

namespace Datateal.Core.Tests.Deployment;

public class OrchestratorDeploymentClientTests
{
    private sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw exception;
    }

    private sealed class SingleClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private static IHttpClientFactory ConnectionFailureFactory() =>
        new SingleClientFactory(new HttpClient(new ThrowingHandler(new HttpRequestException("connection refused")))
        {
            BaseAddress = new Uri("https://orchestrator.invalid"),
        });

    private static IHttpClientFactory TimeoutFactory() =>
        new SingleClientFactory(new HttpClient(new ThrowingHandler(new TaskCanceledException("timed out", new TimeoutException())))
        {
            BaseAddress = new Uri("https://orchestrator.invalid"),
        });

    [Fact]
    public async Task PlanJobsAsync_OrchestratorUnreachable_ThrowsOrchestratorUnavailable()
    {
        var factory = ConnectionFailureFactory();

        await Assert.ThrowsAsync<DeploymentOrchestratorUnavailableException>(() =>
            OrchestratorDeploymentClient.PlanJobsAsync(
                factory, Guid.NewGuid(), [new JobModel { Name = "job1" }], actingUserId: "user-1", CancellationToken.None));
    }

    [Fact]
    public async Task ApplyJobsAsync_OrchestratorTimesOut_ThrowsOrchestratorUnavailable()
    {
        var factory = TimeoutFactory();

        await Assert.ThrowsAsync<DeploymentOrchestratorUnavailableException>(() =>
            OrchestratorDeploymentClient.ApplyJobsAsync(
                factory, Guid.NewGuid(), [new JobModel { Name = "job1" }], actingUserId: "user-1", CancellationToken.None));
    }

    [Fact]
    public async Task ExportJobsAsync_OrchestratorUnreachable_ThrowsOrchestratorUnavailable()
    {
        var factory = ConnectionFailureFactory();

        await Assert.ThrowsAsync<DeploymentOrchestratorUnavailableException>(() =>
            OrchestratorDeploymentClient.ExportJobsAsync(factory, Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task PlanJobsAsync_CallerCancelsRequest_ThrowsOperationCanceled_NotOrchestratorUnavailable()
    {
        // The caller's own cancellation must propagate as an ordinary cancellation, not be
        // reclassified as an orchestrator connectivity failure.
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var factory = new SingleClientFactory(new HttpClient(new ThrowingHandler(new TaskCanceledException("canceled", new OperationCanceledException())))
        {
            BaseAddress = new Uri("https://orchestrator.invalid"),
        });

        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            OrchestratorDeploymentClient.PlanJobsAsync(
                factory, Guid.NewGuid(), [new JobModel { Name = "job1" }], actingUserId: "user-1", cts.Token));
    }
}
