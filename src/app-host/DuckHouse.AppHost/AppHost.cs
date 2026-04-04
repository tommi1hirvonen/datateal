var builder = DistributedApplication.CreateBuilder(args);

var controlPlane = builder.AddProject<Projects.DuckHouse_ControlPlane>("control-plane");

builder.AddProject<Projects.DuckHouse_Ui_Server>("ui")
    .WithReference(controlPlane);

builder.Build().Run();
