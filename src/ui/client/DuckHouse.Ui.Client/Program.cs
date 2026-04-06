using DuckHouse.Ui.Client.Services;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddAntDesign();

builder.Services.AddHttpClient<INodeService, NodeService>(
    client => client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress));
builder.Services.AddHttpClient<IKernelService, KernelService>(
    client => client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress));

builder.Services.AddScoped<IThemeService, ThemeService>();

await builder.Build().RunAsync();
