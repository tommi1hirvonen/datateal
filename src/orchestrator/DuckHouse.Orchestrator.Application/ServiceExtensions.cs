using DuckHouse.Core.Mediator;
using DuckHouse.Orchestrator.Application.Engine;
using DuckHouse.Orchestrator.Application.Yaml;
using Microsoft.Extensions.DependencyInjection;

namespace DuckHouse.Orchestrator.Application;

public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddApplicationServices()
        {
            services.AddMediator<ScanEntryPoint>();

            // Engine services
            services.AddSingleton<RunDispatcher>();
            services.AddSingleton<WarmPoolManager>();
            services.AddScoped<RunCoordinator>();
            services.AddScoped<TaskExecutor>();
            services.AddHostedService<SchedulerService>();
            services.AddHostedService<RecoveryService>();
            services.AddHostedService<WarmPoolReplenishmentService>();
            services.AddHostedService<HistoryRetentionService>();

            // YAML import/export
            services.AddScoped<YamlJobSerializer>();
            services.AddScoped<YamlJobImporter>();

            return services;
        }
    }
}

file class ScanEntryPoint;

