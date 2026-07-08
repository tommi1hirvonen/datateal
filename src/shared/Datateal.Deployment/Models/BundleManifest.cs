namespace Datateal.Deployment.Models;

/// <summary>
/// Top-level <c>datateal.yml</c> manifest that identifies the bundle scope and target.
/// </summary>
public class BundleManifest
{
    /// <summary><c>admin</c> or <c>workspace</c>.</summary>
    public string Scope { get; set; } = "";

    /// <summary>For workspace bundles: the natural workspace name.</summary>
    public string? TargetWorkspace { get; set; }

    /// <summary>
    /// Optional variable definitions. Values can be overridden at deploy time via
    /// <c>${var.NAME}</c> substitution.
    /// </summary>
    public Dictionary<string, string>? Variables { get; set; }
}
