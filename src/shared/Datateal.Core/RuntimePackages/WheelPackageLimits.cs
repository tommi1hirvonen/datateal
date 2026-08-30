namespace Datateal.Core.RuntimePackages;

/// <summary>
/// Shared size limits for wheel package uploads, enforced both by the direct upload endpoint
/// (<c>WheelPackagesController</c>) and by workspace deployment bundle validation
/// (<c>WorkspaceBundleValidator</c>) so a bundle can't be used to smuggle in a wheel binary
/// larger than what the direct upload path allows.
/// </summary>
public static class WheelPackageLimits
{
    public const long MaxFileSizeBytes = 5 * 1024 * 1024;
}
