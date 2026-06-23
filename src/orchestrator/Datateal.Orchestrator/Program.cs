using Datateal.Auth;
using Datateal.Data;
using Datateal.Orchestrator.Application;
using Datateal.Orchestrator.Endpoints;
using Datateal.Orchestrator.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddOpenApi();

builder.Services.AddProblemDetails();

builder.Services.AddDatatealApiKeyAuthentication(builder.Configuration);

var expectedApiKey = builder.Configuration["ServiceAuth:ExpectedApiKey"];
if (string.IsNullOrEmpty(expectedApiKey))
    throw new InvalidOperationException(
        "ServiceAuth:ExpectedApiKey must be configured with a real API key. " +
        "Set it via environment variable, user secrets, or a deployment-specific appsettings override.");

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

builder.AddNpgsqlDbContext<DatatealDbContext>("datateal-ui");

builder.Services.AddDataProtection()
    .PersistKeysToDbContext<DatatealDbContext>()
    .SetApplicationName("Datateal");

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DatatealDbContext>();
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

var api = app.MapGroup(string.Empty).RequireAuthorization();
api.MapJobEndpoints();
api.MapRunEndpoints();
api.MapNodePoolEndpoints();
api.MapAdminEndpoints();

app.Run();
