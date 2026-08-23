namespace Datateal.Ui.Shared.Deployment;

public record ChangeSetDto(
    string Scope,
    string Target,
    bool DryRun,
    List<ResourceChangeDto> Changes,
    ChangeSetSummaryDto Summary);

public record ResourceChangeDto(
    string ResourceType,
    string ResourceName,
    string ChangeType,
    List<FieldChangeDto>? Details = null);

/// <summary>
/// A single field-level before/after value within a <see cref="ResourceChangeDto"/>. Populated
/// for resource types whose natural key aggregates several independently-meaningful sub-entries
/// (e.g. the members of a workspace, or the catalogs a user can access) so a plan/apply result can
/// show exactly which sub-entry changed instead of only "this resource was updated".
/// </summary>
public record FieldChangeDto(string Field, string? Before, string? After);

public record ChangeSetSummaryDto(int Create, int Update, int Delete, int NoChange);
