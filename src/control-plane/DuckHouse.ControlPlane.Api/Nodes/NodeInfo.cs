namespace DuckHouse.ControlPlane.Api.Nodes;

public record NodeInfo(string Name, string Status, string? VmSize = null);
