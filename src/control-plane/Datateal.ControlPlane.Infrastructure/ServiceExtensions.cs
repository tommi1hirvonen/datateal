using System.Security.Cryptography.X509Certificates;
using System.Text;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.ContainerService;
using Datateal.ControlPlane.Core.Repositories;
using Datateal.ControlPlane.Core.Services;
using Datateal.ControlPlane.Infrastructure.Data;
using Datateal.ControlPlane.Infrastructure.Nodes.Aks;
using Datateal.ControlPlane.Infrastructure.Nodes.Kernels;
using Datateal.ControlPlane.Infrastructure.Nodes.Local;
using k8s;
using k8s.Authentication;
using k8s.KubeConfigModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Datateal.ControlPlane.Infrastructure;

public static class ServiceExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RuntimeAuthOptions>().BindConfiguration(RuntimeAuthOptions.Section);

        var nodeBackend = configuration.GetValue("NodeService:Backend", "Local");

        if (string.Equals(nodeBackend, "Aks", StringComparison.OrdinalIgnoreCase))
        {
            services.AddOptions<AksNodeOptions>().BindConfiguration(AksNodeOptions.Section);

            // Shared credential used by both the ARM client and the Kubernetes token provider.
            services.AddSingleton<TokenCredential>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<AksNodeOptions>>().Value;
                return options.HasServicePrincipal
                    ? new ClientSecretCredential(options.TenantId, options.ClientId, options.ClientSecret)
                    : new DefaultAzureCredential();
            });
            services.AddSingleton(sp =>
                new ArmClient(sp.GetRequiredService<TokenCredential>()));
            services.AddSingleton<ITokenProvider>(sp =>
                new AksTokenProvider(sp.GetRequiredService<TokenCredential>()));

            // Fetch the user kubeconfig to obtain the API server URL and CA certificate, then
            // replace the kubelogin exec plugin with a TokenProvider that acquires Entra ID tokens
            // from the registered credential. This works with disableLocalAccounts=true and
            // requires no tooling — locally via az login / SP, in production via managed identity.
            services.AddSingleton<IKubernetes>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<AksNodeOptions>>().Value;
                var tokenProvider = sp.GetRequiredService<ITokenProvider>();
                var clusterId = ContainerServiceManagedClusterResource.CreateResourceIdentifier(
                    options.SubscriptionId, options.ResourceGroupName, options.ClusterName);
                var cluster = sp.GetRequiredService<ArmClient>()
                    .GetContainerServiceManagedClusterResource(clusterId);

                var kubeconfigBytes = cluster.GetClusterUserCredentials().Value.Kubeconfigs[0].Value;
                var parsed = KubernetesYaml.Deserialize<K8SConfiguration>(Encoding.UTF8.GetString(kubeconfigBytes));
                var clusterEntry = parsed.Clusters.First().ClusterEndpoint;

                return new Kubernetes(new KubernetesClientConfiguration
                {
                    Host = clusterEntry.Server,
                    SslCaCerts = new X509Certificate2Collection(
                        X509CertificateLoader.LoadCertificate(
                            Convert.FromBase64String(clusterEntry.CertificateAuthorityData))),
                    TokenProvider = tokenProvider,
                });
            });
            services.AddSingleton(sp => (Kubernetes)sp.GetRequiredService<IKubernetes>());
            services.AddSingleton<INodeService, AksNodeService>();
        }
        else
        {
            services.AddOptions<LocalNodeOptions>().BindConfiguration(LocalNodeOptions.Section);
            services.AddSingleton<IKubernetes>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<LocalNodeOptions>>().Value;
                var config = KubernetesClientConfiguration.BuildConfigFromConfigFile(
                    KubernetesClientConfiguration.KubeConfigDefaultLocation,
                    currentContext: options.KubeContext);
                return new Kubernetes(config);
            });
            services.AddSingleton(sp => (Kubernetes)sp.GetRequiredService<IKubernetes>());
            services.AddSingleton<INodeService, LocalNodeService>();
        }

        services.AddSingleton<INodeRuntimeClient>(sp =>
        {
            var runtimeAuth = sp.GetRequiredService<IOptions<RuntimeAuthOptions>>().Value;
            return new KubernetesRuntimeClient(
                sp.GetRequiredService<Kubernetes>(),
                sp.GetService<ITokenProvider>(),
                runtimeAuth.ApiKey);
        });

        services.AddScoped<INodeConfigRepository, NodeConfigRepository>();

        return services;
    }
}
