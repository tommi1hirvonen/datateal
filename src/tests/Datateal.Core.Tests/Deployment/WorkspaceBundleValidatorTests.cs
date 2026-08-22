using Datateal.Deployment.Models;
using Datateal.Deployment.Serialization;
using Datateal.Ui.Server.Infrastructure.Deployment;
using Xunit;

namespace Datateal.Core.Tests.Deployment;

public class WorkspaceBundleValidatorTests
{
    [Fact]
    public void Validate_Fails_WhenJobReferencesNotebookDeletedFromWorkspace()
    {
        var bundle = new Bundle
        {
            Manifest = new BundleManifest { Scope = "workspace" },
            Notebooks =
            [
                new NotebookModel { Path = "analysis.py", SourceFile = "analysis.py" }
            ],
            Jobs =
            [
                new JobModel
                {
                    Name = "ETL Job",
                    Tasks =
                    [
                        new JobTaskModel
                        {
                            Name = "Task 1",
                            Type = "notebook",
                            NotebookPath = "old_deleted_notebook.py",
                            NodePoolRef = "default-pool"
                        }
                    ]
                }
            ],
            NodePools = [new NodePoolModel { Name = "default-pool", VmSize = "Standard_D2s_v3" }],
            Files = new Dictionary<string, byte[]>
            {
                ["analysis.py"] = "print('hello')"u8.ToArray()
            }
        };

        // Even if old_deleted_notebook.py exists in the workspace prior to deployment,
        // it will be deleted by workspace reconciliation because it is not in bundle.Notebooks.
        var ex = Assert.Throws<InvalidOperationException>(() =>
            WorkspaceBundleValidator.Validate(
                bundle,
                existingNotebookPaths: ["old_deleted_notebook.py"],
                existingQueryPaths: [],
                existingNodePoolNames: [],
                existingEnvironmentVariableKeys: [],
                existingSecretKeys: [],
                existingWheelPackageNames: [],
                existingJobNames: []));

        Assert.Contains("references notebook 'old_deleted_notebook.py' which does not exist", ex.Message);
    }

    [Fact]
    public void Validate_Succeeds_WhenAllCrossReferencesExistInBundle()
    {
        var bundle = new Bundle
        {
            Manifest = new BundleManifest { Scope = "workspace" },
            Notebooks = [new NotebookModel { Path = "etl.py", SourceFile = "etl.py" }],
            NodePools = [new NodePoolModel { Name = "job-pool", VmSize = "Standard_D2s_v3" }],
            Jobs =
            [
                new JobModel
                {
                    Name = "Daily Pipeline",
                    Tasks =
                    [
                        new JobTaskModel
                        {
                            Name = "Run ETL",
                            Type = "notebook",
                            NotebookPath = "etl.py",
                            NodePoolRef = "job-pool"
                        }
                    ]
                }
            ],
            Files = new Dictionary<string, byte[]>
            {
                ["etl.py"] = "print('etl')"u8.ToArray()
            }
        };

        // Should not throw
        WorkspaceBundleValidator.Validate(
            bundle,
            existingNotebookPaths: [],
            existingQueryPaths: [],
            existingNodePoolNames: [],
            existingEnvironmentVariableKeys: [],
            existingSecretKeys: [],
            existingWheelPackageNames: [],
            existingJobNames: []);
    }
}
