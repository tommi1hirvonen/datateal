using System.Text.Json.Serialization;
using Datateal.Auth;
using Datateal.ControlPlane;
using Datateal.ControlPlane.Application;
using Datateal.ControlPlane.Application.InactivityEviction;
using Datateal.ControlPlane.Endpoints;
using Datateal.ControlPlane.Infrastructure;
using Datateal.ControlPlane.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddOpenApi();

builder.Services.AddExceptionHandler<RuntimeProxyExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddDatatealApiKeyAuthentication(builder.Configuration);

var expectedApiKey = builder.Configuration["ServiceAuth:ExpectedApiKey"];
if (string.IsNullOrEmpty(expectedApiKey))
    throw new InvalidOperationException(
        "ServiceAuth:ExpectedApiKey must be configured with a real API key. " +
        "Set it via environment variable, user secrets, or a deployment-specific appsettings override.");

builder.Services.Configure<InactivityEvictionOptions>(
    builder.Configuration.GetSection("InactivityEviction"));

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

builder.AddNpgsqlDbContext<ControlPlaneDbContext>("datateal-control-plane");

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
