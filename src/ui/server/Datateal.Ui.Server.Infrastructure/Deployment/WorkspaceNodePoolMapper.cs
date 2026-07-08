using Datateal.Deployment.Diff;
using Datateal.Deployment.Models;
using Datateal.Orchestrator.Core.Entities;

namespace Datateal.Ui.Server.Infrastructure.Deployment;

internal sealed class WorkspaceNodePoolMapper : IResourceMapper<NodePoolModel>
{
    public string ResourceType => "node_pool";

    public NodePoolModel ToModel(
        NodePoolConfig pool,
        IReadOnlyDictionary<Guid, string> wheelNames,
        IReadOnlyDictionary<Guid, string> variableNames,
        IReadOnlyDictionary<Guid, string> secretNames)
    {
        return new NodePoolModel
        {
            Name = pool.Name,
            Type = pool is InteractiveNodePoolConfig ? "interactive" : "job",
            VmSize = pool.VmSize,
            KernelRequirements = pool.KernelRequirements,
            Description = pool.Description,
            NodeIdleTimeout = pool.NodeIdleTimeout?.ToString("c"),
            WarmNodes = pool is JobNodePoolConfig jobPool ? jobPool.WarmNodes : null,
            MaxNodes = pool is JobNodePoolConfig jobPool2 ? jobPool2.MaxNodes : null,
            NodeAcquireTimeout = pool is JobNodePoolConfig jobPool3 ? jobPool3.NodeAcquireTimeout?.ToString("c") : null,
            WheelPackages = ResolveNames(pool.WheelPackageIds, wheelNames),
            EnvironmentVariables = ResolveNames(pool.EnvironmentVariableIds, variableNames),
            Secrets = ResolveNames(pool.SecretIds, secretNames),
        };
    }

    public string NaturalKey(NodePoolModel model) => model.Name.Trim();

    public bool AreEqual(NodePoolModel desired, NodePoolModel current) =>
        string.Equals(NaturalKey(desired), NaturalKey(current), StringComparison.OrdinalIgnoreCase)
        && string.Equals((desired.Type ?? "job").Trim(), (current.Type ?? "job").Trim(), StringComparison.OrdinalIgnoreCase)
        && string.Equals(desired.VmSize, current.VmSize, StringComparison.Ordinal)
        && string.Equals(desired.KernelRequirements, current.KernelRequirements, StringComparison.Ordinal)
        && string.Equals(desired.Description, current.Description, StringComparison.Ordinal)
        && NullableTimeSpanEquals(desired.NodeIdleTimeout, current.NodeIdleTimeout)
        && NullableTimeSpanEquals(desired.NodeAcquireTimeout, current.NodeAcquireTimeout)
        && NullableIntEquals(desired.WarmNodes, current.WarmNodes)
        && NullableIntEquals(desired.MaxNodes, current.MaxNodes)
        && NormalizeList(desired.WheelPackages).SequenceEqual(NormalizeList(current.WheelPackages), StringComparer.OrdinalIgnoreCase)
        && NormalizeList(desired.EnvironmentVariables).SequenceEqual(NormalizeList(current.EnvironmentVariables), StringComparer.OrdinalIgnoreCase)
        && NormalizeList(desired.Secrets).SequenceEqual(NormalizeList(current.Secrets), StringComparer.OrdinalIgnoreCase);

    private static bool NullableTimeSpanEquals(string? desired, string? current)
    {
        if (string.IsNullOrWhiteSpace(desired) && string.IsNullOrWhiteSpace(current))
            return true;

        return TimeSpan.TryParse(desired, out var desiredValue)
            && TimeSpan.TryParse(current, out var currentValue)
            && desiredValue == currentValue;
    }

    private static bool NullableIntEquals(int? desired, int? current) => (desired ?? 0) == (current ?? 0);

    private static List<string> ResolveNames(List<Guid>? ids, IReadOnlyDictionary<Guid, string> namesById) =>
        (ids ?? [])
            .Where(namesById.ContainsKey)
            .Select(id => namesById[id])
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static List<string> NormalizeList(List<string>? values) =>
        (values ?? [])
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
