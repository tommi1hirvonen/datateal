using System.Text.Json;
using Datateal.Deployment.Models;
using Datateal.Deployment.Serialization;
using Datateal.Ui.Server.Application.Mediator.Commands;
using Xunit;

namespace Datateal.Core.Tests.Deployment;

public class ApplyWorkspaceDeploymentLogRedactionTests
{
    private static Bundle BuildBundleWithSecretsAndFiles() => new()
    {
        Manifest = new BundleManifest { Scope = "workspace" },
        Secrets =
        [
            new SecretModel { Key = "inline-secret", Description = "literal value", Value = "super-secret-plaintext" },
            new SecretModel { Key = "env-secret", Description = "env reference", Value = "${env.MY_SECRET}" },
            new SecretModel { Key = "unchanged-secret", Description = "no value supplied", Value = null },
        ],
        WheelPackages =
        [
            new WheelPackageModel { Name = "mypackage", FileName = "mypackage.whl", BundleFilePath = "files/wheels/mypackage.whl" },
        ],
        Notebooks =
        [
            new NotebookModel { Path = "etl", SourceFile = "src/notebooks/etl.ipynb" },
        ],
        Files = new Dictionary<string, byte[]>
        {
            ["files/wheels/mypackage.whl"] = [1, 2, 3, 4, 5],
            ["src/notebooks/etl.ipynb"] = "{\"cells\": []}"u8.ToArray(),
        },
    };

    [Fact]
    public void RedactForLogging_ClearsFiles()
    {
        var bundle = BuildBundleWithSecretsAndFiles();

        var redacted = ApplyWorkspaceDeploymentHandler.RedactForLogging(bundle);

        Assert.Empty(redacted.Files);
    }

    [Fact]
    public void RedactForLogging_RedactsLiteralSecretValues_ButPreservesNullAndReferences()
    {
        var bundle = BuildBundleWithSecretsAndFiles();

        var redacted = ApplyWorkspaceDeploymentHandler.RedactForLogging(bundle);

        var inline = Assert.Single(redacted.Secrets, s => s.Key == "inline-secret");
        Assert.Equal("<redacted>", inline.Value);

        var envRef = Assert.Single(redacted.Secrets, s => s.Key == "env-secret");
        Assert.Equal("<redacted>", envRef.Value); // still redacted: any non-null value is hidden regardless of form

        var unchanged = Assert.Single(redacted.Secrets, s => s.Key == "unchanged-secret");
        Assert.Null(unchanged.Value);
    }

    [Fact]
    public void RedactForLogging_PreservesOtherResourceMetadata()
    {
        var bundle = BuildBundleWithSecretsAndFiles();

        var redacted = ApplyWorkspaceDeploymentHandler.RedactForLogging(bundle);

        Assert.Single(redacted.WheelPackages, w => w.Name == "mypackage");
        Assert.Single(redacted.Notebooks, n => n.Path == "etl");
    }

    [Fact]
    public void RedactForLogging_DoesNotMutateOriginalBundle()
    {
        var bundle = BuildBundleWithSecretsAndFiles();

        ApplyWorkspaceDeploymentHandler.RedactForLogging(bundle);

        Assert.NotEmpty(bundle.Files);
        Assert.Equal("super-secret-plaintext", bundle.Secrets.Single(s => s.Key == "inline-secret").Value);
    }

    [Fact]
    public void SerializedRedactedBundle_ContainsNeitherWheelBytesNorSecretPlaintext()
    {
        var bundle = BuildBundleWithSecretsAndFiles();

        var redacted = ApplyWorkspaceDeploymentHandler.RedactForLogging(bundle);
        var json = JsonSerializer.Serialize(redacted);

        Assert.DoesNotContain("super-secret-plaintext", json);
        Assert.DoesNotContain("cells", json); // notebook source content must not be embedded
        Assert.DoesNotContain("\"Files\":{\"", json); // Files dictionary must be empty, not populated
    }
}
