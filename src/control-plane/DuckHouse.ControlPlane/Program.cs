using DuckHouse.Auth;
using DuckHouse.ControlPlane;
using DuckHouse.ControlPlane.Application;
using DuckHouse.ControlPlane.Application.InactivityEviction;
using DuckHouse.ControlPlane.Endpoints;
using DuckHouse.ControlPlane.Infrastructure;
using DuckHouse.ControlPlane.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddOpenApi();

builder.Services.AddExceptionHandler<RuntimeProxyExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddDuckHouseApiKeyAuthentication(builder.Configuration);

var expectedApiKey = builder.Configuration["ServiceAuth:ExpectedApiKey"];
if (string.IsNullOrEmpty(expectedApiKey))
    throw new InvalidOperationException(
        "ServiceAuth:ExpectedApiKey must be configured with a real API key. " +
        "Set it via environment variable, user secrets, or a deployment-specific appsettings override.");

builder.Services.Configure<InactivityEvictionOptions>(
    builder.Configuration.GetSection("InactivityEviction"));

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

builder.AddNpgsqlDbContext<ControlPlaneDbContext>("duckhouse-control-plane");

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
    await db.Database.CreateExecutionStrategy().ExecuteAsync(() => db.Database.MigrateAsync());
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();
app.MapGroup(string.Empty).RequireAuthorization().MapNodeEndpoints();

app.Run();
