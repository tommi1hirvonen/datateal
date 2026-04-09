var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres");
var controlPlaneDb = postgres.AddDatabase("duckhouse-control-plane");

var controlPlane = builder.AddProject<Projects.DuckHouse_ControlPlane>("control-plane")
    .WithReference(controlPlaneDb)
    .WaitFor(controlPlaneDb);

builder.AddProject<Projects.DuckHouse_Ui_Server>("ui")
    .WithReference(controlPlane);

builder.Build().Run();
