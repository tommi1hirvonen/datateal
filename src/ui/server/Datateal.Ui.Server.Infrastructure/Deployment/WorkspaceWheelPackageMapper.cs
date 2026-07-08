using Datateal.Core.RuntimePackages;
using Datateal.Deployment.Diff;
using Datateal.Deployment.Models;
using Datateal.Deployment.Serialization;

namespace Datateal.Ui.Server.Infrastructure.Deployment;

internal sealed class WorkspaceWheelPackageMapper : IResourceMapper<WheelPackageModel>
{
    private readonly Dictionary<string, byte[]> _currentDataByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, byte[]> _desiredDataByName = new(StringComparer.OrdinalIgnoreCase);

    public string ResourceType => "wheel_package";

    public void LoadDesired(Bundle bundle)
    {
        _desiredDataByName.Clear();
        foreach (var model in bundle.WheelPackages)
        {
            if (string.IsNullOrWhiteSpace(model.BundleFilePath) || !bundle.Files.TryGetValue(model.BundleFilePath, out var bytes))
                continue;

            _desiredDataByName[NaturalKey(model)] = bytes;
        }
    }

    public WheelPackageModel ToModel(WheelPackage package)
    {
        _currentDataByName[package.Name] = package.Data;
        return new WheelPackageModel
        {
            Name = package.Name,
            FileName = package.FileName,
            BundleFilePath = DeploymentPathHelpers.GetWheelBundleFilePath(package.FileName),
        };
    }

    public string NaturalKey(WheelPackageModel model) => model.Name.Trim();

    public bool AreEqual(WheelPackageModel desired, WheelPackageModel current)
    {
        var key = NaturalKey(desired);
        return string.Equals(key, NaturalKey(current), StringComparison.OrdinalIgnoreCase)
            && string.Equals(desired.FileName, current.FileName, StringComparison.Ordinal)
            && _desiredDataByName.TryGetValue(key, out var desiredBytes)
            && _currentDataByName.TryGetValue(key, out var currentBytes)
            && desiredBytes.SequenceEqual(currentBytes);
    }
}
