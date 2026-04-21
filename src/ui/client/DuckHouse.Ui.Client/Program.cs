using DuckHouse.Ui.Client.Services;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

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

builder.Services.AddScoped<IThemeService, ThemeService>();
builder.Services.AddScoped<IRecentItemsService, RecentItemsService>();

await builder.Build().RunAsync();
