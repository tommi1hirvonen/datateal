var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.DuckHouse_ControlPlane_Api>("api");

builder.AddProject<Projects.DuckHouse_Ui>("ui")
    .WithReference(api);

builder.Build().Run();
