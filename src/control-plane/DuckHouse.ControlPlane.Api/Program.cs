using Azure.Identity;
using Azure.ResourceManager;
using DuckHouse.ControlPlane.Api.Nodes;
using DuckHouse.ControlPlane.Api.Nodes.Aks;
using DuckHouse.ControlPlane.Api.Nodes.Local;
using k8s;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var nodeBackend = builder.Configuration.GetValue("NodeService:Backend", "Local");
if (string.Equals(nodeBackend, "Aks", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddOptions<AksNodeOptions>().BindConfiguration(AksNodeOptions.Section);
    builder.Services.AddSingleton(_ => new ArmClient(new DefaultAzureCredential()));
    builder.Services.AddScoped<INodeService, AksNodeService>();
}
else
{
    builder.Services.AddSingleton<IKubernetes>(_ =>
    {
        var config = KubernetesClientConfiguration.BuildConfigFromConfigFile(
            KubernetesClientConfiguration.KubeConfigDefaultLocation);
        return new Kubernetes(config);
    });
    builder.Services.AddScoped<INodeService, LocalNodeService>();
}

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
