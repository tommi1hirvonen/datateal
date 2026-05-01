namespace DuckHouse.Auth;

/// <summary>
/// Application role name constants.
/// </summary>
public static class DuckHouseRole
{
    public const string Admin = nameof(Admin);
    public const string NodePoolContributor = nameof(NodePoolContributor);
    public const string NodePoolOperator = nameof(NodePoolOperator);
    public const string JobContributor = nameof(JobContributor);
    public const string JobOperator = nameof(JobOperator);
    public const string JobReader = nameof(JobReader);
    public const string WorkspaceContributor = nameof(WorkspaceContributor);
    public const string CatalogContributor = nameof(CatalogContributor);
    public const string EnvironmentManager = nameof(EnvironmentManager);

    public static readonly string[] All =
    [
        Admin,
        NodePoolContributor,
        NodePoolOperator,
        JobContributor,
        JobOperator,
        JobReader,
        WorkspaceContributor,
        CatalogContributor,
        EnvironmentManager
    ];
}
