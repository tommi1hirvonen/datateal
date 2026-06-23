using Datateal.Core.Mediator;
using Datateal.Ui.Server.Application.Ai;
using Microsoft.Extensions.DependencyInjection;

namespace Datateal.Ui.Server.Application;

public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddApplicationServices()
        {
            services.AddMediator<ScanEntryPoint>();
            services.AddScoped<IAiChatService, AiChatService>();
            services.AddScoped<IAiAgentService, AiAgentService>();
            services.AddScoped<IContextAssembler, ContextAssembler>();
            return services;
        }
    }
}

file class ScanEntryPoint;
