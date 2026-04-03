using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Microsoft.Extensions.Options;
using DuckHouse.ControlPlane.Api.Nodes;
using DuckHouse.ControlPlane.Api.Nodes.Aks;
using DuckHouse.ControlPlane.Api.Nodes.Kernels;
using DuckHouse.ControlPlane.Api.Nodes.Local;
using k8s;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var nodeBackend = builder.Configuration.GetValue("NodeService:Backend", "Local");

builder.Services.AddSingleton<IKubernetes>(_ =>
{
    var config = KubernetesClientConfiguration.BuildConfigFromConfigFile(
        KubernetesClientConfiguration.KubeConfigDefaultLocation);
    return new Kubernetes(config);
});
// Expose the concrete type so KubernetesRuntimeClient can access HttpClient.
builder.Services.AddSingleton(sp => (Kubernetes)sp.GetRequiredService<IKubernetes>());
builder.Services.AddScoped<INodeRuntimeClient, KubernetesRuntimeClient>();

if (string.Equals(nodeBackend, "Aks", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddOptions<AksNodeOptions>().BindConfiguration(AksNodeOptions.Section);
    builder.Services.AddSingleton(sp =>
    {
        var options = sp.GetRequiredService<IOptions<AksNodeOptions>>().Value;
        TokenCredential credential = options.HasServicePrincipal
            ? new ClientSecretCredential(options.TenantId, options.ClientId, options.ClientSecret)
            : new DefaultAzureCredential();
        return new ArmClient(credential);
    });
    builder.Services.AddScoped<INodeService, AksNodeService>();
}
else
{
    builder.Services.AddScoped<INodeService, LocalNodeService>();
}

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
