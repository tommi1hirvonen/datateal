using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.ContainerService;
using Microsoft.Extensions.Options;
using DuckHouse.ControlPlane.Api.Nodes;
using DuckHouse.ControlPlane.Api.Nodes.Aks;
using DuckHouse.ControlPlane.Api.Nodes.Kernels;
using DuckHouse.ControlPlane.Api.Nodes.Local;
using k8s;
using k8s.Authentication;
using k8s.KubeConfigModels;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var nodeBackend = builder.Configuration.GetValue("NodeService:Backend", "Local");

if (string.Equals(nodeBackend, "Aks", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddOptions<AksNodeOptions>().BindConfiguration(AksNodeOptions.Section);

    // Shared credential used by both the ARM client and the Kubernetes token provider.
    builder.Services.AddSingleton<TokenCredential>(sp =>
    {
        var options = sp.GetRequiredService<IOptions<AksNodeOptions>>().Value;
        return options.HasServicePrincipal
            ? new ClientSecretCredential(options.TenantId, options.ClientId, options.ClientSecret)
            : new DefaultAzureCredential();
    });
    builder.Services.AddSingleton(sp =>
        new ArmClient(sp.GetRequiredService<TokenCredential>()));
    builder.Services.AddSingleton<ITokenProvider>(sp =>
        new AksTokenProvider(sp.GetRequiredService<TokenCredential>()));

    // Fetch the user kubeconfig to obtain the API server URL and CA certificate, then
    // replace the kubelogin exec plugin with a TokenProvider that acquires Entra ID tokens
    // from the registered credential. This works with disableLocalAccounts=true and
    // requires no tooling — locally via az login / SP, in production via managed identity.
    builder.Services.AddSingleton<IKubernetes>(sp =>
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
    builder.Services.AddSingleton(sp => (Kubernetes)sp.GetRequiredService<IKubernetes>());
    builder.Services.AddScoped<INodeService, AksNodeService>();
}
else
{
    builder.Services.AddOptions<LocalNodeOptions>().BindConfiguration(LocalNodeOptions.Section);
    builder.Services.AddSingleton<IKubernetes>(sp =>
    {
        var options = sp.GetRequiredService<IOptions<LocalNodeOptions>>().Value;
        var config = KubernetesClientConfiguration.BuildConfigFromConfigFile(
            KubernetesClientConfiguration.KubeConfigDefaultLocation,
            currentContext: options.KubeContext);
        return new Kubernetes(config);
    });
    builder.Services.AddSingleton(sp => (Kubernetes)sp.GetRequiredService<IKubernetes>());
    builder.Services.AddScoped<INodeService, LocalNodeService>();
}

builder.Services.AddScoped<INodeRuntimeClient>(sp =>
    new KubernetesRuntimeClient(
        sp.GetRequiredService<Kubernetes>(),
        sp.GetService<ITokenProvider>()));

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapDefaultEndpoints();
app.MapNodeEndpoints();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
