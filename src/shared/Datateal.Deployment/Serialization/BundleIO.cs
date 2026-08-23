using System.IO.Compression;
using Datateal.Deployment.Models;

namespace Datateal.Deployment.Serialization;

/// <summary>
/// Represents a loaded bundle as a strongly-typed model graph. All file contents
/// are held in memory (suitable for CI/CD payloads; not for streaming).
/// </summary>
public class Bundle
{
    public required BundleManifest Manifest { get; init; }

    // Admin-scope resources
    public List<CatalogModel> Catalogs { get; init; } = [];
    public List<WorkspaceModel> Workspaces { get; init; } = [];
    public List<WorkspaceMembershipModel> Memberships { get; init; } = [];
    public List<UserCatalogAccessModel> UserCatalogAccess { get; init; } = [];

    // Workspace-scope resources
    public List<FolderModel> Folders { get; init; } = [];
    public List<NotebookModel> Notebooks { get; init; } = [];
    public List<QueryModel> Queries { get; init; } = [];
    public List<NodePoolModel> NodePools { get; init; } = [];
    public List<EnvironmentVariableModel> EnvironmentVariables { get; init; } = [];
    public List<SecretModel> Secrets { get; init; } = [];
    public List<WheelPackageModel> WheelPackages { get; init; } = [];
    public List<JobModel> Jobs { get; init; } = [];

    /// <summary>
    /// Raw file contents keyed by bundle-relative path (e.g. <c>src/notebooks/etl/load.ipynb</c>).
    /// Used to read notebook/query source and wheel binary payloads.
    /// </summary>
    public Dictionary<string, byte[]> Files { get; init; } = [];
}

/// <summary>
/// Reads a ZIP bundle into a <see cref="Bundle"/> model graph.
/// </summary>
public static class BundleReader
{
    /// <summary>Reads a ZIP archive from a stream.</summary>
    public static Bundle ReadZip(Stream zipStream)
    {
        ZipArchive archive;
        try
        {
            archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true);
        }
        catch (InvalidDataException ex)
        {
            throw new InvalidOperationException("Bundle upload is not a valid ZIP archive.", ex);
        }

        using (archive)
            return ReadArchive(archive);
    }

    /// <summary>Reads a ZIP archive from raw bytes.</summary>
    public static Bundle ReadZip(byte[] zipBytes)
    {
        using var ms = new MemoryStream(zipBytes);
        return ReadZip(ms);
    }

    private static Bundle ReadArchive(ZipArchive archive)
    {
        // Index all files by path.
        var files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Entries)
        {
            if (entry.FullName.EndsWith('/')) continue;
            using var stream = entry.Open();
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            files[entry.FullName] = ms.ToArray();
        }

        // Read and validate manifest
        if (!files.TryGetValue("manifest.yml", out var manifestBytes))
            throw new InvalidOperationException("Bundle is missing 'manifest.yml'.");

        var manifest = BundleYaml.Deserialize<BundleManifest>(
            System.Text.Encoding.UTF8.GetString(manifestBytes));

        if (string.IsNullOrWhiteSpace(manifest.Scope))
            throw new InvalidOperationException("Bundle manifest is missing 'scope'.");

        var bundle = new Bundle { Manifest = manifest, Files = files };

        if (manifest.Scope.Equals("admin", StringComparison.OrdinalIgnoreCase))
            ReadAdminResources(files, bundle);
        else if (manifest.Scope.Equals("workspace", StringComparison.OrdinalIgnoreCase))
            ReadWorkspaceResources(files, bundle);
        else
            throw new InvalidOperationException($"Unknown bundle scope '{manifest.Scope}'. Expected 'admin' or 'workspace'.");

        return bundle;
    }

    private static void ReadAdminResources(Dictionary<string, byte[]> files, Bundle bundle)
    {
        foreach (var (path, bytes) in files)
        {
            var text = System.Text.Encoding.UTF8.GetString(bytes);
            if (path.StartsWith("resources/catalogs/", StringComparison.OrdinalIgnoreCase) && path.EndsWith(".catalog.yml"))
                bundle.Catalogs.Add(BundleYaml.Deserialize<CatalogModel>(text));
            else if (path.StartsWith("resources/workspaces/", StringComparison.OrdinalIgnoreCase) && path.EndsWith(".workspace.yml"))
                bundle.Workspaces.Add(BundleYaml.Deserialize<WorkspaceModel>(text));
            else if (path.Equals("resources/permissions/memberships.yml", StringComparison.OrdinalIgnoreCase))
            {
                var list = BundleYaml.Deserialize<List<WorkspaceMembershipModel>>(text);
                bundle.Memberships.AddRange(list ?? []);
            }
            else if (path.Equals("resources/permissions/catalog-access.yml", StringComparison.OrdinalIgnoreCase))
            {
                var list = BundleYaml.Deserialize<List<UserCatalogAccessModel>>(text);
                bundle.UserCatalogAccess.AddRange(list ?? []);
            }
        }
    }

    private static void ReadWorkspaceResources(Dictionary<string, byte[]> files, Bundle bundle)
    {
        var notebooksByPath = new Dictionary<string, NotebookModel>(StringComparer.OrdinalIgnoreCase);
        var queriesByPath = new Dictionary<string, QueryModel>(StringComparer.OrdinalIgnoreCase);

        foreach (var (path, bytes) in files)
        {
            if (path.StartsWith("files/") || path.StartsWith("src/")) continue;
            var text = System.Text.Encoding.UTF8.GetString(bytes);

            if (path.StartsWith("resources/node_pools/", StringComparison.OrdinalIgnoreCase) && path.EndsWith(".nodepool.yml"))
                bundle.NodePools.Add(BundleYaml.Deserialize<NodePoolModel>(text));
            else if (path.StartsWith("resources/jobs/", StringComparison.OrdinalIgnoreCase) && path.EndsWith(".job.yml"))
                bundle.Jobs.Add(BundleYaml.Deserialize<JobModel>(text));
            else if (path.StartsWith("resources/notebooks/", StringComparison.OrdinalIgnoreCase) && path.EndsWith(".notebook.yml"))
            {
                var model = BundleYaml.Deserialize<NotebookModel>(text);
                notebooksByPath[model.Path] = model;
            }
            else if (path.StartsWith("resources/queries/", StringComparison.OrdinalIgnoreCase) && path.EndsWith(".query.yml"))
            {
                var model = BundleYaml.Deserialize<QueryModel>(text);
                queriesByPath[model.Path] = model;
            }
            else if (path.Equals("resources/environment/variables.yml", StringComparison.OrdinalIgnoreCase))
            {
                var list = BundleYaml.Deserialize<List<EnvironmentVariableModel>>(text);
                bundle.EnvironmentVariables.AddRange(list ?? []);
            }
            else if (path.Equals("resources/environment/secrets.yml", StringComparison.OrdinalIgnoreCase))
            {
                var list = BundleYaml.Deserialize<List<SecretModel>>(text);
                bundle.Secrets.AddRange(list ?? []);
            }
            else if (path.StartsWith("resources/wheel_packages/", StringComparison.OrdinalIgnoreCase) && path.EndsWith(".yml"))
                bundle.WheelPackages.Add(BundleYaml.Deserialize<WheelPackageModel>(text));
            else if (path.Equals("resources/folders.yml", StringComparison.OrdinalIgnoreCase))
            {
                var list = BundleYaml.Deserialize<List<FolderModel>>(text);
                bundle.Folders.AddRange(list ?? []);
            }
        }

        // Notebooks and queries are discovered from src/ files and enriched from metadata YAML when present.
        foreach (var (path, bytes) in files)
        {
            if (path.StartsWith("src/notebooks/", StringComparison.OrdinalIgnoreCase))
            {
                var relativePath = path["src/notebooks/".Length..];
                var logicalPath = StripExtension(relativePath);
                if (!notebooksByPath.TryGetValue(logicalPath, out var model))
                {
                    model = new NotebookModel { Path = logicalPath };
                    notebooksByPath[logicalPath] = model;
                }

                model.SourceFile = path;
            }
            else if (path.StartsWith("src/queries/", StringComparison.OrdinalIgnoreCase))
            {
                var relativePath = path["src/queries/".Length..];
                var logicalPath = StripExtension(relativePath);
                if (!queriesByPath.TryGetValue(logicalPath, out var model))
                {
                    model = new QueryModel { Path = logicalPath };
                    queriesByPath[logicalPath] = model;
                }

                model.SourceFile = path;
            }
        }

        bundle.Notebooks.AddRange(notebooksByPath.Values);
        bundle.Queries.AddRange(queriesByPath.Values);
    }

    private static string StripExtension(string path)
    {
        var ext = Path.GetExtension(path);
        return string.IsNullOrEmpty(ext) ? path : path[..^ext.Length];
    }
}

/// <summary>
/// Writes a <see cref="Bundle"/> model graph to a ZIP archive.
/// </summary>
public static class BundleWriter
{
    /// <summary>Writes the bundle to a new ZIP in-memory and returns the bytes.</summary>
    public static byte[] WriteZip(Bundle bundle)
    {
        using var ms = new MemoryStream();
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            WriteArchive(archive, bundle);
        return ms.ToArray();
    }

    private static void WriteArchive(ZipArchive archive, Bundle bundle)
    {
        // Track written paths to avoid duplicates (archive.Entries is not available in Create mode).
        var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Write(string path, string text) { WriteEntry(archive, path, text); written.Add(path); }
        void WriteRaw(string path, byte[] bytes) { WriteEntry(archive, path, bytes); written.Add(path); }

        Write("manifest.yml", BundleYaml.Serialize(bundle.Manifest));

        if (bundle.Manifest.Scope.Equals("admin", StringComparison.OrdinalIgnoreCase))
            WriteAdminResources(archive, bundle, Write);
        else
            WriteWorkspaceResources(archive, bundle, Write);

        // Append raw binary files (wheel payloads, etc.) not yet written.
        foreach (var (path, bytes) in bundle.Files)
        {
            if (!written.Contains(path))
                WriteRaw(path, bytes);
        }
    }

    private static void WriteAdminResources(ZipArchive archive, Bundle bundle, Action<string, string> write)
    {
        foreach (var catalog in bundle.Catalogs)
            write($"resources/catalogs/{Slug(catalog.Name)}.catalog.yml", BundleYaml.Serialize(catalog));

        foreach (var ws in bundle.Workspaces)
            write($"resources/workspaces/{Slug(ws.Name)}.workspace.yml", BundleYaml.Serialize(ws));

        if (bundle.Memberships.Count > 0)
            write("resources/permissions/memberships.yml", BundleYaml.Serialize(bundle.Memberships));

        if (bundle.UserCatalogAccess.Count > 0)
            write("resources/permissions/catalog-access.yml", BundleYaml.Serialize(bundle.UserCatalogAccess));
    }

    private static void WriteWorkspaceResources(ZipArchive archive, Bundle bundle, Action<string, string> write)
    {
        foreach (var pool in bundle.NodePools)
            write($"resources/node_pools/{Slug(pool.Name)}.nodepool.yml", BundleYaml.Serialize(pool));

        foreach (var job in bundle.Jobs)
            write($"resources/jobs/{Slug(job.Name)}.job.yml", BundleYaml.Serialize(job));

        foreach (var notebook in bundle.Notebooks)
            write($"resources/notebooks/{Slug(notebook.Path)}.notebook.yml", BundleYaml.Serialize(notebook));

        foreach (var query in bundle.Queries)
            write($"resources/queries/{Slug(query.Path)}.query.yml", BundleYaml.Serialize(query));

        if (bundle.EnvironmentVariables.Count > 0)
            write("resources/environment/variables.yml", BundleYaml.Serialize(bundle.EnvironmentVariables));

        if (bundle.Secrets.Count > 0)
            write("resources/environment/secrets.yml", BundleYaml.Serialize(bundle.Secrets));

        foreach (var wheel in bundle.WheelPackages)
            write($"resources/wheel_packages/{Slug(wheel.Name)}.yml", BundleYaml.Serialize(wheel));

        if (bundle.Folders.Count > 0)
            write("resources/folders.yml", BundleYaml.Serialize(bundle.Folders));
    }

    private static void WriteEntry(ZipArchive archive, string path, string text)
        => WriteEntry(archive, path, System.Text.Encoding.UTF8.GetBytes(text));

    private static void WriteEntry(ZipArchive archive, string path, byte[] bytes)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        stream.Write(bytes);
    }

    private static string Slug(string name) =>
        name.ToLowerInvariant()
            .Replace(' ', '_')
            .Replace('/', '_')
            .Replace('\\', '_');
}
