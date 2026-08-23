using Datateal.Deployment.Models;
using Datateal.Deployment.Serialization;

namespace Datateal.Core.Tests.Deployment;

public class BundleIoTests
{
    // ── Admin bundle round-trip ────────────────────────────────────────────────

    [Fact]
    public void AdminBundle_RoundTrip_PreservesManifest()
    {
        var bundle = new Bundle
        {
            Manifest = new BundleManifest { Scope = "admin" },
            Catalogs = [new CatalogModel { Name = "sales_prod", Type = "managed", DataPath = "/data/sales" }],
            Workspaces = [new WorkspaceModel { Name = "Sales" }],
            Memberships =
            [
                new WorkspaceMembershipModel
                {
                    Workspace = "Sales",
                    Members = [new WorkspaceMemberEntry { Email = "alice@example.com", Roles = ["WorkspaceAdmin"] }],
                }
            ],
        };

        var zip = BundleWriter.WriteZip(bundle);
        var readBack = BundleReader.ReadZip(zip);

        Assert.Equal("admin", readBack.Manifest.Scope);
        Assert.Single(readBack.Catalogs, c => c.Name == "sales_prod" && c.DataPath == "/data/sales");
        Assert.Single(readBack.Workspaces, w => w.Name == "Sales");
        Assert.Single(readBack.Memberships, m => m.Workspace == "Sales" && m.Members[0].Email == "alice@example.com");
    }

    // ── Workspace bundle round-trip ───────────────────────────────────────────

    [Fact]
    public void WorkspaceBundle_RoundTrip_PreservesNodePools()
    {
        var bundle = new Bundle
        {
            Manifest = new BundleManifest { Scope = "workspace", TargetWorkspace = "Sales (Dev)" },
            NodePools =
            [
                new NodePoolModel { Name = "default-job-pool", Type = "job", VmSize = "Standard_D4s_v3", WarmNodes = 1 }
            ],
            EnvironmentVariables =
            [
                new EnvironmentVariableModel { Key = "DB_HOST", Value = "postgres.example.com", Description = "Database host" }
            ],
            Secrets =
            [
                new SecretModel { Key = "DB_PASSWORD" }
            ],
        };

        var zip = BundleWriter.WriteZip(bundle);
        var readBack = BundleReader.ReadZip(zip);

        Assert.Equal("workspace", readBack.Manifest.Scope);
        Assert.Equal("Sales (Dev)", readBack.Manifest.TargetWorkspace);
        Assert.Single(readBack.NodePools, p => p.Name == "default-job-pool" && p.WarmNodes == 1);
        Assert.Single(readBack.EnvironmentVariables, v => v.Key == "DB_HOST");
        Assert.Single(readBack.Secrets, s => s.Key == "DB_PASSWORD");
    }

    [Fact]
    public void WorkspaceBundle_IncludesWheelBinaryPayload()
    {
        var wheelBytes = new byte[] { 0x50, 0x4B, 0x05, 0x06 }; // fake PK header
        var bundle = new Bundle
        {
            Manifest = new BundleManifest { Scope = "workspace", TargetWorkspace = "Sales" },
            WheelPackages = [new WheelPackageModel { Name = "shared_utils", FileName = "shared_utils-1.0.0-py3-none-any.whl", BundleFilePath = "files/wheels/shared_utils-1.0.0-py3-none-any.whl" }],
            Files = new Dictionary<string, byte[]>
            {
                ["files/wheels/shared_utils-1.0.0-py3-none-any.whl"] = wheelBytes,
            },
        };

        var zip = BundleWriter.WriteZip(bundle);
        var readBack = BundleReader.ReadZip(zip);

        Assert.True(readBack.Files.ContainsKey("files/wheels/shared_utils-1.0.0-py3-none-any.whl"));
        Assert.Equal(wheelBytes, readBack.Files["files/wheels/shared_utils-1.0.0-py3-none-any.whl"]);
    }

    // ── Missing manifest error ────────────────────────────────────────────────

    [Fact]
    public void ReadZip_MissingManifest_Throws()
    {
        using var ms = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("some_file.txt");
            using var s = entry.Open();
            s.Write(System.Text.Encoding.UTF8.GetBytes("hello"));
        }

        ms.Position = 0;
        var ex = Assert.Throws<InvalidOperationException>(() => BundleReader.ReadZip(ms));
        Assert.Contains("manifest.yml", ex.Message);
    }

    // ── Malformed upload errors ───────────────────────────────────────────────

    [Fact]
    public void ReadZip_NotAZipArchive_ThrowsInvalidOperationException()
    {
        // Arbitrary non-ZIP bytes (e.g. a user accidentally uploading a random file) must be
        // reported as a normal 400-mappable InvalidOperationException, not a raw
        // System.IO.InvalidDataException bubbling up as an unhandled 500.
        var garbage = System.Text.Encoding.UTF8.GetBytes("this is definitely not a zip file");

        var ex = Assert.Throws<InvalidOperationException>(() => BundleReader.ReadZip(garbage));
        Assert.Contains("not a valid ZIP archive", ex.Message);
    }

    [Fact]
    public void ReadZip_MalformedManifestYaml_ThrowsInvalidOperationException()
    {
        using var ms = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("manifest.yml");
            using var s = entry.Open();
            // Invalid YAML: unterminated flow mapping.
            s.Write(System.Text.Encoding.UTF8.GetBytes("scope: [workspace"));
        }

        ms.Position = 0;
        var ex = Assert.Throws<InvalidOperationException>(() => BundleReader.ReadZip(ms));
        Assert.Contains("could not be parsed", ex.Message);
    }

    // ── Zip-bomb decompression caps ───────────────────────────────────────────

    [Fact]
    public void ReadZip_SingleEntryExceedsPerEntryCap_ThrowsInvalidOperationException()
    {
        // Highly-compressible content (all zeros) so the entry is tiny on the wire but decodes to
        // just over the per-entry cap — the guard must count actual decompressed bytes, not trust
        // zip metadata.
        var oversized = new byte[BundleLimits.MaxEntryDecompressedBytes + 1024];

        using var ms = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("files/wheels/huge.bin", System.IO.Compression.CompressionLevel.Optimal);
            using var s = entry.Open();
            s.Write(oversized);
        }

        ms.Position = 0;
        var ex = Assert.Throws<InvalidOperationException>(() => BundleReader.ReadZip(ms));
        Assert.Contains("exceeds the maximum allowed decompressed size", ex.Message);
    }

    [Fact]
    public void ReadZip_TotalDecompressedSizeExceedsCap_ThrowsInvalidOperationException()
    {
        // Four entries, each individually under the per-entry cap, but summing past the total cap.
        const int perEntryBytes = 38 * 1024 * 1024;
        var chunk = new byte[perEntryBytes];

        using var ms = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            for (var i = 0; i < 4; i++)
            {
                var entry = archive.CreateEntry($"files/wheels/part{i}.bin", System.IO.Compression.CompressionLevel.Optimal);
                using var s = entry.Open();
                s.Write(chunk);
            }
        }

        ms.Position = 0;
        var ex = Assert.Throws<InvalidOperationException>(() => BundleReader.ReadZip(ms));
        Assert.Contains("exceeds the maximum total decompressed size", ex.Message);
    }

    [Fact]
    public void ReadZip_WithinDecompressionCaps_StillReadsSuccessfully()
    {
        // Regression guard: an ordinary small bundle must not be affected by the new caps.
        var bundle = new Bundle
        {
            Manifest = new BundleManifest { Scope = "workspace", TargetWorkspace = "Sales" },
            EnvironmentVariables = [new EnvironmentVariableModel { Key = "DB_HOST", Value = "postgres.example.com" }],
        };

        var zip = BundleWriter.WriteZip(bundle);
        var readBack = BundleReader.ReadZip(zip);

        Assert.Equal("workspace", readBack.Manifest.Scope);
        Assert.Single(readBack.EnvironmentVariables, v => v.Key == "DB_HOST");
    }
}
