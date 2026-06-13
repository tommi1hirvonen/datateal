using Datateal.Ui.Client.Services;
using Datateal.Ui.Shared.Users;
using Datateal.Ui.Shared.Workspaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddAuthorizationCore(DatatealAuthorizationPolicies.Configure);
builder.Services.AddAuthenticationStateDeserialization();

// Active-workspace state shared by services and the authorization handler.
builder.Services.AddSingleton<ActiveWorkspaceProvider>();
builder.Services.AddSingleton<IActiveWorkspaceAccessor>(sp => sp.GetRequiredService<ActiveWorkspaceProvider>());
builder.Services.AddScoped<IAuthorizationHandler, WorkspaceScopedRoleHandler>();

builder.Services.AddAntDesign();

builder.Services.AddHttpClient<INodeService, NodeService>(
    client => client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress));
builder.Services.AddHttpClient<IKernelService, KernelService>(
    client => client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress));
builder.Services.AddHttpClient<IWorkspaceService, WorkspaceService>(
    client => client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress));
builder.Services.AddHttpClient<IJobService, JobService>(
    client => client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress));
builder.Services.AddHttpClient<INodePoolService, NodePoolService>(
    client => client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress));
builder.Services.AddHttpClient<IInteractivePoolService, InteractivePoolService>(
    client => client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress));
builder.Services.AddHttpClient<IWheelPackageService, WheelPackageService>(
    client => client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress));
builder.Services.AddHttpClient<IEnvironmentService, EnvironmentService>(
    client => client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress));
builder.Services.AddHttpClient<ICatalogService, CatalogService>(
    client => client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress));
builder.Services.AddHttpClient<IUserService, UserService>(
    client => client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress));
builder.Services.AddHttpClient<IWorkspaceManagementService, WorkspaceManagementService>(
    client => client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress));

builder.Services.AddScoped<IThemeService, ThemeService>();
builder.Services.AddScoped<IRecentItemsService, RecentItemsService>();
builder.Services.AddScoped<IAiAssistantService, AiAssistantService>();
builder.Services.AddScoped<IAiCredentialService, AiCredentialService>();
builder.Services.AddScoped<IAiSettingsService, AiSettingsService>();

await builder.Build().RunAsync();
