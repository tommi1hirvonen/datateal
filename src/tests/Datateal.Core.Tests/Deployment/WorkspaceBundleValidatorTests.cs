using Datateal.Core.RuntimePackages;
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
            WorkspaceBundleValidator.Validate(bundle));

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
        WorkspaceBundleValidator.Validate(bundle);
    }

    [Fact]
    public void Validate_Fails_WhenWheelPackageExceedsMaxSize()
    {
        var oversizedData = new byte[WheelPackageLimits.MaxFileSizeBytes + 1];
        var bundle = new Bundle
        {
            Manifest = new BundleManifest { Scope = "workspace" },
            WheelPackages =
            [
                new WheelPackageModel { Name = "too-big", FileName = "too-big.whl", BundleFilePath = "files/wheels/too-big.whl" }
            ],
            Files = new Dictionary<string, byte[]>
            {
                ["files/wheels/too-big.whl"] = oversizedData
            }
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            WorkspaceBundleValidator.Validate(bundle));

        Assert.Contains("too-big", ex.Message);
        Assert.Contains("exceeds the maximum size", ex.Message);
    }

    [Fact]
    public void Validate_Succeeds_WhenWheelPackageWithinMaxSize()
    {
        var data = new byte[WheelPackageLimits.MaxFileSizeBytes];
        var bundle = new Bundle
        {
            Manifest = new BundleManifest { Scope = "workspace" },
            WheelPackages =
            [
                new WheelPackageModel { Name = "just-right", FileName = "just-right.whl", BundleFilePath = "files/wheels/just-right.whl" }
            ],
            Files = new Dictionary<string, byte[]>
            {
                ["files/wheels/just-right.whl"] = data
            }
        };

        // Should not throw
        WorkspaceBundleValidator.Validate(bundle);
    }

    [Fact]
    public void Validate_Fails_WhenEnvironmentVariableReferencesUnsuppliedEnvToken()
    {
        var bundle = new Bundle
        {
            Manifest = new BundleManifest { Scope = "workspace" },
            EnvironmentVariables =
            [
                new EnvironmentVariableModel { Key = "DB_HOST", Value = "${env.MISSING_HOST}" }
            ],
        };

        // No env dictionary supplied — the token cannot resolve, and this must be caught during
        // Plan (dryRun), not only surface later during Apply.
        var ex = Assert.Throws<InvalidOperationException>(() =>
            WorkspaceBundleValidator.Validate(bundle, variables: null, env: null));

        Assert.Contains("MISSING_HOST", ex.Message);
    }

    [Fact]
    public void Validate_Fails_WhenSecretValueReferencesUnsuppliedVarToken()
    {
        var bundle = new Bundle
        {
            Manifest = new BundleManifest { Scope = "workspace" },
            Secrets =
            [
                new SecretModel { Key = "API_KEY", Value = "${var.missing_var}" }
            ],
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            WorkspaceBundleValidator.Validate(bundle, variables: new Dictionary<string, string>(), env: null));

        Assert.Contains("missing_var", ex.Message);
    }

    [Fact]
    public void Validate_Succeeds_WhenAllTokensResolve()
    {
        var bundle = new Bundle
        {
            Manifest = new BundleManifest { Scope = "workspace" },
            EnvironmentVariables =
            [
                new EnvironmentVariableModel { Key = "DB_HOST", Value = "${var.db_host}" }
            ],
            Secrets =
            [
                new SecretModel { Key = "API_KEY", Value = "${env.API_KEY}" },
                new SecretModel { Key = "UNCHANGED", Value = null },
            ],
        };

        var variables = new Dictionary<string, string> { ["db_host"] = "postgres.example.com" };
        var env = new Dictionary<string, string> { ["API_KEY"] = "secret-value" };

        // Should not throw
        WorkspaceBundleValidator.Validate(bundle, variables, env);
    }
}
