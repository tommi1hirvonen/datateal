namespace Datateal.Deployment.Serialization;

/// <summary>
/// Decompressed-size limits enforced while reading a bundle ZIP archive
/// (<see cref="BundleReader"/>), independent of the compressed upload size cap enforced by the
/// HTTP layer (<c>DeploymentController.MaxBundleSizeBytes</c>). These guard against zip-bomb
/// style archives that are small on the wire but expand to an unbounded size in memory.
/// </summary>
public static class BundleLimits
{
    /// <summary>Maximum decompressed size of any single ZIP entry.</summary>
    public const long MaxEntryDecompressedBytes = 50L * 1024 * 1024;

    /// <summary>Maximum total decompressed size across all ZIP entries combined.</summary>
    public const long MaxTotalDecompressedBytes = 150L * 1024 * 1024;
}
