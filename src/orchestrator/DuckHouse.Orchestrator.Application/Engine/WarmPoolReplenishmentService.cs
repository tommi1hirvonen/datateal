using DuckHouse.Orchestrator.Core.Entities;
using DuckHouse.Orchestrator.Core.Interfaces;
using DuckHouse.Orchestrator.Core.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DuckHouse.Orchestrator.Application.Engine;

/// <summary>
/// Initialises <see cref="WarmPoolManager"/> on startup by discovering existing warm nodes,
/// then periodically replenishes each pool's warm standby count in case nodes were
/// evicted or lost between restarts.
/// </summary>
public class WarmPoolReplenishmentService(
    WarmPoolManager warmPoolManager,
    IServiceScopeFactory scopeFactory,
    ILogger<WarmPoolReplenishmentService> logger) : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DefaultReplenishInterval = TimeSpan.FromSeconds(60);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Allow RecoveryService (3 s delay) and other startup services to settle first.
        await Task.Delay(StartupDelay, stoppingToken);

        logger.LogInformation("WarmPoolReplenishmentService: starting warm pool initialisation");

        await RunReplenishmentCycleAsync(initialise: true, stoppingToken);

        logger.LogInformation("WarmPoolReplenishmentService: warm pool initialised; entering periodic replenishment loop");

        using var timer = new PeriodicTimer(DefaultReplenishInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunReplenishmentCycleAsync(initialise: false, stoppingToken);
        }
    }

    private async Task RunReplenishmentCycleAsync(bool initialise, CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var sp = scope.ServiceProvider;

            var nodePoolRepo = sp.GetRequiredService<INodePoolConfigRepository>();
            var controlPlane = sp.GetRequiredService<IControlPlaneClient>();
            var wheelReader = sp.GetRequiredService<IWheelPackageReader>();
            var envResolver = sp.GetRequiredService<IEnvironmentResolver>();

            var allConfigs = await nodePoolRepo.GetAllAsync(ct);
            var warmPools = allConfigs
                .OfType<JobNodePoolConfig>()
                .Where(c => c.WarmNodes > 0 || c.MaxNodes.HasValue)
                .ToList();

            if (warmPools.Count == 0) return;

            if (initialise)
                await warmPoolManager.InitialiseAsync(warmPools, controlPlane, ct);

            // Replenish each pool (fills any gap caused by eviction or restart).
            var replenishTasks = warmPools
                .Where(c => c.WarmNodes > 0)
                .Select(c => warmPoolManager.ReplenishAsync(c, controlPlane, wheelReader, envResolver));

            await Task.WhenAll(replenishTasks);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "WarmPoolReplenishmentService: error during replenishment cycle");
        }
    }
}
