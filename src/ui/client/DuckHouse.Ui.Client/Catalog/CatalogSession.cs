using DuckHouse.Core.Kernels;
using DuckHouse.Ui.Client.Services;
using DuckHouse.Ui.Shared.Catalogs;
using DuckHouse.Ui.Shared.Kernels;

namespace DuckHouse.Ui.Client.Catalog;

/// <summary>
/// Encapsulates the kernel-side catalog attach/detach lifecycle for a single interactive page session.
/// Construct one instance per page, supplying service references and lambdas that read the page's
/// current kernel/node identifiers. All catalog state and operations flow through this object.
/// </summary>
internal sealed class CatalogSession
{
    private readonly IKernelService _kernelService;
    private readonly ICatalogService _catalogService;
    private readonly Func<string?> _nodeName;
    private readonly Func<string?> _kernelId;
    private readonly Func<IReadOnlyList<CatalogDto>> _allCatalogs;
    private readonly Action _notifyStateChanged;
    private readonly Action<string> _setError;

    private HashSet<string> _attachedNames = new(StringComparer.OrdinalIgnoreCase);

    public bool SetupRunning { get; private set; }

    private bool HasKernel => !string.IsNullOrEmpty(_kernelId());

    public CatalogSession(
        IKernelService kernelService,
        ICatalogService catalogService,
        Func<string?> nodeName,
        Func<string?> kernelId,
        Func<IReadOnlyList<CatalogDto>> allCatalogs,
        Action notifyStateChanged,
        Action<string> setError)
    {
        _kernelService = kernelService;
        _catalogService = catalogService;
        _nodeName = nodeName;
        _kernelId = kernelId;
        _allCatalogs = allCatalogs;
        _notifyStateChanged = notifyStateChanged;
        _setError = setError;
    }

    /// <summary>Clears attached-catalog tracking when the kernel is disconnected.</summary>
    public void Clear() => _attachedNames.Clear();

    /// <summary>
    /// Called when the user changes the catalog selection in the sidebar.
    /// Persists the new selection to the workspace, then — when a kernel is connected —
    /// syncs actual attach state from DuckDB and applies the minimal ATTACH/DETACH operations.
    /// </summary>
    public async Task OnSelectionChangedAsync(
        IEnumerable<CatalogDto> selectedItems,
        Guid? workspaceItemId)
    {
        var names = selectedItems.Select(c => c.Name).ToList();

        if (workspaceItemId.HasValue)
        {
            try
            {
                await _catalogService.UpdateWorkspaceItemCatalogsAsync(
                    workspaceItemId.Value, names);
            }
            catch (Exception ex) { _setError($"Failed to save catalog selection: {ex.Message}"); }
        }

        if (!HasKernel) return;

        // Sync from the kernel so manual ATTACH/DETACH the user ran in a cell is
        // reflected in _attachedNames before we compute what to change.
        await SyncAsync();

        var desired = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
        var toDetach = _attachedNames.Except(desired, StringComparer.OrdinalIgnoreCase).ToList();
        var toAttach = desired.Except(_attachedNames, StringComparer.OrdinalIgnoreCase).ToList();

        foreach (var name in toDetach)
            await DetachAsync(name);

        foreach (var name in toAttach)
            await AttachAsync(name);
    }

    /// <summary>
    /// Attaches all provided catalogs at once using a single setup script executed server-side.
    /// Called at connect time when the user already has catalogs selected.
    /// </summary>
    public async Task SetupAllAsync(IEnumerable<string> names)
    {
        var nameList = names.ToList();
        if (nameList.Count == 0 || !HasKernel) return;

        SetupRunning = true;
        _notifyStateChanged();

        try
        {
            var handle = await _catalogService.SetupCatalogsOnKernelAsync(
                _nodeName()!, _kernelId()!, nameList);

            var poll = await PollAsync(handle.ExecutionId);

            if (poll.Result?.Error is not null)
                _setError($"Catalog setup failed: {poll.Result.Error.Evalue}");
            else
                _attachedNames = new HashSet<string>(nameList, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex) { _setError($"Catalog setup failed: {ex.Message}"); }
        finally
        {
            SetupRunning = false;
            _notifyStateChanged();
        }
    }

    /// <summary>
    /// Queries DuckDB for the currently attached databases and refreshes the tracking set
    /// so manual ATTACH/DETACH commands run by the user are reflected before diffing.
    /// Silently falls back to the cached set on any error.
    /// </summary>
    private async Task SyncAsync()
    {
        if (!HasKernel) return;

        // Underscore-prefixed aliases to avoid polluting the user's kernel namespace.
        const string script = """
            import duckdb as _dh_ddb, json as _dh_json
            print("__dh_dbs__:" + _dh_json.dumps([r[0] for r in _dh_ddb.execute("SELECT database_name FROM duckdb_databases() WHERE NOT internal AND database_name != 'memory'").fetchall()]))
            del _dh_ddb, _dh_json
            """;

        try
        {
            var handle = await _kernelService.StartExecuteAsync(
                _nodeName()!, _kernelId()!, new ExecuteKernelRequest(script));
            var poll = await PollAsync(handle.ExecutionId, pollDelayMs: 500);
            if (poll.Result is null || poll.Result.Error is not null) return;

            var knownNames = new HashSet<string>(
                _allCatalogs().Select(c => c.Name), StringComparer.OrdinalIgnoreCase);

            var streamText = string.Concat(
                poll.Result.Outputs
                    .Where(o => o.Type == "stream" && o.Text is not null)
                    .Select(o => o.Text));

            var sentinelLine = streamText
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(l => l.StartsWith("__dh_dbs__:", StringComparison.Ordinal));

            if (sentinelLine is null) return;

            var parsedNames = System.Text.Json.JsonSerializer.Deserialize<List<string>>(
                sentinelLine["__dh_dbs__:".Length..]);

            if (parsedNames is null) return;

            _attachedNames = new HashSet<string>(
                parsedNames.Where(n => knownNames.Contains(n)),
                StringComparer.OrdinalIgnoreCase);
        }
        catch { /* fall back to cached state on any error */ }
    }

    private async Task AttachAsync(string name)
    {
        if (!HasKernel) return;

        SetupRunning = true;
        _notifyStateChanged();

        try
        {
            var handle = await _catalogService.ConnectCatalogOnKernelAsync(
                _nodeName()!, _kernelId()!, name);

            var poll = await PollAsync(handle.ExecutionId);

            if (poll.Result?.Error is not null)
                _setError($"Catalog attach failed: {poll.Result.Error.Evalue}");
            else
                _attachedNames.Add(name);
        }
        catch (Exception ex) { _setError($"Catalog attach failed: {ex.Message}"); }
        finally
        {
            SetupRunning = false;
            _notifyStateChanged();
        }
    }

    private async Task DetachAsync(string name)
    {
        if (!HasKernel) return;

        SetupRunning = true;
        _notifyStateChanged();

        try
        {
            var handle = await _catalogService.DisconnectCatalogOnKernelAsync(
                _nodeName()!, _kernelId()!, name);

            var poll = await PollAsync(handle.ExecutionId);

            if (poll.Result?.Error is not null)
                _setError($"Catalog detach failed: {poll.Result.Error.Evalue}");
            else
                _attachedNames.Remove(name);
        }
        catch (Exception ex) { _setError($"Catalog detach failed: {ex.Message}"); }
        finally
        {
            SetupRunning = false;
            _notifyStateChanged();
        }
    }

    private async Task<PollExecutionResponse> PollAsync(string executionId, int pollDelayMs = 1000)
    {
        PollExecutionResponse poll;
        do
        {
            await Task.Delay(pollDelayMs);
            poll = await _kernelService.PollExecutionAsync(_nodeName()!, _kernelId()!, executionId);
        } while (!poll.IsComplete);

        return poll;
    }
}
