using Datateal.Core.Environment;
using Datateal.Deployment.Diff;
using Datateal.Deployment.Models;

namespace Datateal.Ui.Server.Infrastructure.Deployment;

internal sealed class WorkspaceEnvironmentVariableMapper : IResourceMapper<EnvironmentVariableModel>
{
    public string ResourceType => "environment_variable";

    public EnvironmentVariableModel ToModel(EnvironmentVariable variable) => new()
    {
        Key = variable.Key,
        Value = variable.Value,
        Description = variable.Description,
    };

    public string NaturalKey(EnvironmentVariableModel model) => model.Key.Trim();

    public bool AreEqual(EnvironmentVariableModel desired, EnvironmentVariableModel current) =>
        string.Equals(NaturalKey(desired), NaturalKey(current), StringComparison.OrdinalIgnoreCase)
        && string.Equals(desired.Value, current.Value, StringComparison.Ordinal)
        && string.Equals(desired.Description, current.Description, StringComparison.Ordinal);
}
