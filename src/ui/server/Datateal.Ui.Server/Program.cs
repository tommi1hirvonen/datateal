using System.Text.Json.Serialization;
using Datateal.Auth;
using Datateal.Auth.Dev;
using Datateal.Auth.EntraId;
using Datateal.Data;
using Datateal.Ui.Server;
using Datateal.Ui.Server.Application;
using Datateal.Ui.Server.Auth;
using Datateal.Ui.Server.Components;
using Datateal.Ui.Server.Core.Catalogs;
using Datateal.Ui.Server.Infrastructure;
using Datateal.Ui.Shared.Users;
using Datateal.Ui.Shared.Workspaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
	.AddInteractiveWebAssemblyComponents()
	.AddAuthenticationStateSerialization(options => options.SerializeAllClaims = true);

builder.Services.AddControllers()
	.AddJsonOptions(options =>
		options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddExceptionHandler<UpstreamExceptionHandler>();
builder.Services.AddProblemDetails();

// Authentication — pluggable OIDC/dummy provider selected by Authentication:Provider
var authProvider = builder.Configuration["Authentication:Provider"] ?? "EntraId";
if (authProvider.Equals("Dev", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddDevAuthentication();
else
    builder.Services.AddEntraIdAuthentication();
builder.Services.AddDatatealWebAppAuthentication(builder.Configuration);
builder.Services.AddAuthorization(DatatealAuthorizationPolicies.Configure);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IActiveWorkspaceAccessor, HttpActiveWorkspaceAccessor>();
builder.Services.AddScoped<IAuthorizationHandler, WorkspaceScopedRoleHandler>();
builder.Services.Configure<AdminUsersOptions>(builder.Configuration.GetSection("Authorization"));
builder.Services.AddScoped<IClaimsTransformation, AppClaimsTransformation>();

builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

builder.Services.AddSignalR();

builder.Services.Configure<CatalogSettings>(builder.Configuration.GetSection("Catalogs"));

builder.Services.AddDataProtection()
    .PersistKeysToDbContext<DatatealDbContext>()
    .SetApplicationName("Datateal");

builder.AddNpgsqlDbContext<DatatealDbContext>("datateal-ui");

builder.Services.AddOrchestratorProxy(builder.Configuration);

builder.Services.AddAntDesign();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DatatealDbContext>();
    await db.Database.CreateExecutionStrategy().ExecuteAsync(() => db.Database.MigrateAsync());
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
	app.UseWebAssemblyDebugging();
	app.UseExceptionHandler(); // picks up UpstreamExceptionHandler
}
else
{
	app.UseExceptionHandler("/Error", createScopeForErrors: true); // picks up UpstreamExceptionHandler, then falls back to /Error
	// The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
	app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapControllers().RequireAuthorization();
app.MapOrchestratorProxy();
app.MapLoginAndLogout();
app.MapRazorComponents<App>()
	.AddInteractiveWebAssemblyRenderMode()
	.AddAdditionalAssemblies(typeof(Datateal.Ui.Client._Imports).Assembly);

app.MapDefaultEndpoints();
app.MapHub<Datateal.Ui.Server.Hubs.AiAssistantHub>("/ai/hub");

app.Run();