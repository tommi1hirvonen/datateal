using Datateal.Core.Environment;
using Datateal.Deployment.Diff;
using Datateal.Deployment.Models;

namespace Datateal.Ui.Server.Infrastructure.Deployment;

internal sealed class WorkspaceSecretMapper : IResourceMapper<SecretModel>
{
    public string ResourceType => "secret";

    public SecretModel ToModel(Secret secret) => new()
    {
        Key = secret.Key,
        Description = secret.Description,
        Value = null,
    };

    public string NaturalKey(SecretModel model) => model.Key.Trim();

    public bool AreEqual(SecretModel desired, SecretModel current)
    {
        if (!string.Equals(NaturalKey(desired), NaturalKey(current), StringComparison.OrdinalIgnoreCase))
            return false;

        if (desired.Value is not null)
            return false;

        return string.Equals(desired.Description, current.Description, StringComparison.Ordinal);
    }
}
