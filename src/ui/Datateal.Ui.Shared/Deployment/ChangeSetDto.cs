namespace Datateal.Ui.Shared.Deployment;

public record ChangeSetDto(
    string Scope,
    string Target,
    bool DryRun,
    List<ResourceChangeDto> Changes,
    ChangeSetSummaryDto Summary);

public record ResourceChangeDto(string ResourceType, string ResourceName, string ChangeType);

public record ChangeSetSummaryDto(int Create, int Update, int Delete, int NoChange);
