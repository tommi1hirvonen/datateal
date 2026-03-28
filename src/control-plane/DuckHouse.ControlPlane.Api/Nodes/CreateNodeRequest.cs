namespace DuckHouse.ControlPlane.Api.Nodes;

public record CreateNodeRequest(string Name, string? VmSize = null);
