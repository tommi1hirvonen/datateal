using Datateal.Core.Mediator;
using Datateal.Orchestrator.Application.Engine;
using Datateal.Orchestrator.Application.Yaml;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace Datateal.Orchestrator.Application;

public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddApplicationServices()
        {
            services.AddMediator<ScanEntryPoint>();

            // Quartz scheduler
            services.AddQuartz(q => q.UseDefaultThreadPool(tp => tp.MaxConcurrency = 100));
            services.AddQuartzHostedService(opt => opt.WaitForJobsToComplete = true);
            services.AddTransient<ScheduledJobExecutor>();
            services.AddSingleton<SchedulesManager>();
            services.AddHostedService(sp => sp.GetRequiredService<SchedulesManager>());

            // Engine services
            services.AddSingleton<RunDispatcher>();
            services.AddSingleton<WarmPoolManager>();
            services.AddScoped<RunCoordinator>();
            services.AddScoped<TaskExecutor>();
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
