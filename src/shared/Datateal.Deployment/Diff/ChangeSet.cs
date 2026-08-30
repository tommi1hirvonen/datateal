namespace Datateal.Deployment.Diff;

/// <summary>Type of change detected or applied by the diff engine.</summary>
public enum ChangeType
{
    Create,
    Update,
    Delete,
    NoChange,
}

/// <summary>A single resource-level change in a deployment plan or apply result.</summary>
public class ResourceChange
{
    public required string ResourceType { get; init; }
    public required string ResourceName { get; init; }
    public required ChangeType ChangeType { get; init; }

    /// <summary>
    /// Optional field-level details (before/after pairs). Used for resource-diff
    /// previews in the future git-provider integration; populated but not required
    /// by callers that only need the high-level change type.
    /// </summary>
    public List<FieldChange>? Details { get; init; }
}

/// <summary>Before/after value for a single field on a changed resource.</summary>
public class FieldChange
{
    public required string Field { get; init; }
    public string? Before { get; init; }
    public string? After { get; init; }
}

/// <summary>
/// Aggregated result of a deployment plan or apply operation. Returned by both
/// <c>plan</c> (dry-run) and <c>apply</c> endpoints.
/// </summary>
public class ChangeSet
{
    /// <summary><c>admin</c> or <c>workspace</c>.</summary>
    public required string Scope { get; init; }

    /// <summary>Human-readable target name (workspace name for workspace scope, "admin" otherwise).</summary>
    public required string Target { get; init; }

    /// <summary>True when this is a plan (dry-run); false when changes were actually applied.</summary>
    public bool DryRun { get; init; }

    public List<ResourceChange> Changes { get; init; } = [];

    public ChangeSetSummary Summary => new(
        Changes.Count(c => c.ChangeType == ChangeType.Create),
        Changes.Count(c => c.ChangeType == ChangeType.Update),
        Changes.Count(c => c.ChangeType == ChangeType.Delete),
        Changes.Count(c => c.ChangeType == ChangeType.NoChange));
}

/// <summary>Aggregated counts for a <see cref="ChangeSet"/>.</summary>
public record ChangeSetSummary(int Create, int Update, int Delete, int NoChange);
