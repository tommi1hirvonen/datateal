using Datateal.ControlPlane.Application.InactivityEviction;
using Datateal.Core.Mediator;
using Microsoft.Extensions.DependencyInjection;

namespace Datateal.ControlPlane.Application;

public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddApplicationServices()
        {
            services.AddMediator<ScanEntryPoint>();
            services.AddHostedService<InactivityEvictionService>();
            return services;
        }
    }
}

file class ScanEntryPoint;
