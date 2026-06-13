using Datateal.Ui.Shared.Workspaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;

namespace Datateal.Ui.Client.Services;

/// <summary>
/// Holds the client's active workspace and the list of workspaces the signed-in user can
/// access. The active workspace is driven by the <c>/w/{workspaceId}/...</c> URL segment
/// when present (so shared links open the right workspace), falling back to a value
/// persisted in <c>localStorage</c>. Exposes the active id synchronously via
/// <see cref="IActiveWorkspaceAccessor"/> for the API header handler and authorization
/// handler.
/// </summary>
public sealed class ActiveWorkspaceProvider : IActiveWorkspaceAccessor, IDisposable
{
    private const string StorageKey = "datateal-active-workspace";

    private readonly IJSRuntime _js;
    private readonly NavigationManager _navigation;

    public ActiveWorkspaceProvider(IJSRuntime js, NavigationManager navigation)
    {
        _js = js;
        _navigation = navigation;
        _navigation.LocationChanged += OnLocationChanged;
        ApplyUri(_navigation.Uri);
    }

    public Guid? ActiveWorkspaceId { get; private set; }

    public IReadOnlyList<WorkspaceDto> Available { get; private set; } = [];

    public bool IsInitialized { get; private set; }

    /// <summary>Raised when the active workspace or available list changes.</summary>
    public event Action? Changed;

    public WorkspaceDto? Active =>
        ActiveWorkspaceId is { } id ? Available.FirstOrDefault(w => w.Id == id) : null;

    /// <summary>
    /// Loads the available workspaces. If the URL did not already determine the active
    /// workspace, resolves it from the persisted value (when still valid) or the first
    /// available workspace.
    /// </summary>
    public async Task InitializeAsync(IReadOnlyList<WorkspaceDto> available)
    {
        Available = available;

        if (ActiveWorkspaceId is null || available.All(w => w.Id != ActiveWorkspaceId))
        {
            Guid? stored = null;
            try
            {
                var raw = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
                if (Guid.TryParse(raw, out var parsed))
                    stored = parsed;
            }
            catch
            {
                // localStorage may be unavailable; fall back to the first workspace.
            }

            ActiveWorkspaceId = stored is { } s && available.Any(w => w.Id == s)
                ? s
                : available.FirstOrDefault()?.Id;
        }

        IsInitialized = true;
        await PersistAsync();
        Changed?.Invoke();
    }

    /// <summary>Sets the active workspace without navigating (used on non-scoped pages).</summary>
    public async Task SetActiveAsync(Guid workspaceId)
    {
        if (ActiveWorkspaceId == workspaceId)
            return;

        ActiveWorkspaceId = workspaceId;
        await PersistAsync();
        Changed?.Invoke();
    }

    /// <summary>Builds a workspace-scoped URL for the active workspace.</summary>
    public string Link(string suffix) =>
        ActiveWorkspaceId is { } id ? WorkspaceRoute.Build(id, suffix) : "/";

    private void OnLocationChanged(object? sender, LocationChangedEventArgs e) => ApplyUri(e.Location);

    private void ApplyUri(string uri)
    {
        var relative = "/" + _navigation.ToBaseRelativePath(uri);
        if (WorkspaceRoute.TryParse(relative, out var id, out _) && id != ActiveWorkspaceId)
        {
            ActiveWorkspaceId = id;
            _ = PersistAsync();
            Changed?.Invoke();
        }
    }

    private async Task PersistAsync()
    {
        try
        {
            if (ActiveWorkspaceId is { } id)
                await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, id.ToString());
        }
        catch
        {
            // Best-effort persistence.
        }
    }

    public void Dispose() => _navigation.LocationChanged -= OnLocationChanged;
}
