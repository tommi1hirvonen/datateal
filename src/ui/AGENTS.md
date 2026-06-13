# Datateal UI

Blazor Web App: ASP.NET Core server host (`Datateal.Ui.Server`) with a WebAssembly client (`Datateal.Ui.Client`). Uses **Ant Design Blazor** (`AntDesign`) for UI components, **BlazorMonaco** for code editing, and **Markdig** for Markdown rendering. Integrates with .NET Aspire.

## Projects

| Project | Role |
|---|---|
| `Datateal.Ui.Shared` | DTOs shared between server and WASM client (`Nodes/`, `Kernels/`, `Workspace/` subfolders) |
| `Datateal.Ui.Server` | ASP.NET Core host; REST API controllers, Blazor/WASM bootstrap, EF Core migrations |
| `Datateal.Ui.Server.Core` | Domain layer: entity classes, repository interfaces |
| `Datateal.Ui.Server.Application` | Use-case layer: custom mediator pattern (commands + queries) |
| `Datateal.Ui.Server.Infrastructure` | EF Core + SQLite; concrete repositories, `AddInfrastructureServices()` |
| `Datateal.Ui.Client` | WASM client: pages, layouts, typed `HttpClient` services |
| `Datateal.Ui.Client.Components` | Razor Class Library: `CodeCell` (Monaco wrapper), `ExecutionTimer` |

## Architecture

**Server** follows Clean Architecture (Core → Application → Infrastructure → Host). Use cases are implemented via a custom mediator (`IMediator`, `IRequest<T>`, `IRequestHandler<TReq,TRes>`) with commands in `Mediator/Commands/` and queries in `Mediator/Queries/`. REST API controllers in `Datateal.Ui.Server/Controllers/` map requests to mediator calls.

**Hosting**: `App.razor` renders `<Routes>` with `InteractiveWebAssemblyRenderMode(prerender: false)`. All interactive pages live in the WASM client. No prerender.

**Client services**: typed `HttpClient` wrappers in `Datateal.Ui.Client/Services/` call the server REST API. Register all services in `Datateal.Ui.Client/Program.cs`.

## Workspace feature

Workspaces store **notebooks** and **SQL query files** in a folder hierarchy backed by EF Core + SQLite using **TPH inheritance**. `WorkspaceItem` is the abstract EF base class; `Notebook` and `Query` are concrete subclasses. The EF discriminator column is `ItemType varchar(32)`. `Query` adds nullable columns for the last execution result (`LastExecutedAt`, `LastDurationMs`, `LastResultStatus`, `LastResultJson` as a JSON blob).

- Server: `WorkspaceController` at `api/workspaces/{workspaceId:guid}/items`; typed repository methods use `.OfType<Notebook>()` / `.OfType<Query>()`
- Client: `IWorkspaceService` / `WorkspaceService` in `Datateal.Ui.Client/Services/`
- Workspace listing: folders first (alphabetical), then notebooks and queries merged and sorted alphabetically

**WorkspacePage** (`/workspace`): lists folders and items with a Type column; supports create, rename, move, clone, delete for both notebooks and queries.

**QueryPage** (`/query`, `/query/{id}`): split-screen SQL editor (top) + results panel (bottom). A draggable divider uses `initQueryPageSplitter(topPaneId, handleId, dotNetRef)` JS interop; drag is pure JS for smoothness, fires `[JSInvokable] OnSplitterDragEnd(double height)` on release. `QueryPage` implements `IAsyncDisposable` to dispose the `DotNetObjectReference`.

**NotebookPage** (`/notebook`, `/notebook/{id}`): polyglot notebook with Python, SQL, and Markdown cells. SQL cells are wrapped as `import duckdb; duckdb.sql("""...""")` before kernel execution.

## Interactive node pools

Interactive node pools are a class of `NodePoolConfig` with `PoolType = "Interactive"`. They have 0 or 1 live compute nodes at any time. Users connect to them from `NotebookPage` and `QueryPage` by selecting a pool; if the pool's node isn't running, one is created on demand.

- Server: `InteractivePoolsController` at `api/workspaces/{workspaceId:guid}/interactive-pools`; key endpoints:
  - `GET api/workspaces/{workspaceId}/interactive-pools` — lists all `InteractiveNodePoolConfig` entries with live node state (fetched from the control plane per pool)
  - `POST api/workspaces/{workspaceId}/interactive-pools/{name}/ensure-node` — idempotent "start or join" call; returns `NodeInfo`; handles 409 race by retrying GET
- Client: `IInteractivePoolService` / `InteractivePoolService` in `Datateal.Ui.Client/Services/`; registered in `Program.cs`
- DTO: `InteractivePoolDto` in `Datateal.Ui.Shared/Nodes/`; carries `Name`, `VmSize`, `Description`, `Status` (of type `InteractivePoolStatus`)

**Node naming**: `"i" + pool.Id.ToString("N")[..11].ToLowerInvariant()` — stable across pool renames; always 12 characters.

**Job tasks on interactive pools**: if an orchestrator job task references an interactive pool, `NodeManager` joins or creates the pool's node but does **not** delete it when the run completes. Eviction handles teardown.

## Client pages

| Page | Route | Description |
|---|---|---|
| `Home.razor` | `/` | Welcome page |
| `WorkspacePage.razor` | `/workspace` | Workspace browser |
| `NotebookPage.razor` | `/notebook` | Polyglot notebook editor |
| `QueryPage.razor` | `/query` | SQL query editor with results panel |
| `NodePoolsPage.razor` | `/node-pools` | Node pool config management; tabs for Interactive, Job, and Active nodes |
| `Kernels.razor` | `/nodes/{name}/kernels` | Kernel management per node |
| `Settings.razor` | `/settings` | Theme settings |

## Styling

Global CSS in `server/Datateal.Ui.Server/wwwroot/css/app.css` — layout skeleton (`.cp-layout`, `.cp-main`, site header, Monaco cell borders, dark mode tokens). Component-specific styles live in component-scoped `.razor.css` files co-located with their component (e.g., `NotebookPage.razor.css`, `QueryPage.razor.css`, `AiChatPanel.razor.css`). RCL static CSS in `Datateal.Ui.Client.Components/wwwroot/defaults.css`. Dark mode class `ant-dark` is toggled on `<html>`.

**Do not use `var(--ant-*)` CSS custom properties** — they do not exist in this build of Ant Design Blazor. Use hardcoded hex values and target dark mode with `html.ant-dark` selectors in `app.css`. Typical values: borders `#d9d9d9` / `#434343` (dark), subtle backgrounds `#fafafa` / `#1d1d1d` (dark).

## Ant Design Blazor conventions

- Register with `builder.Services.AddAntDesign()` in both server and client `Program.cs`
- Icons: `<Icon Type="@IconType.Outline.X" />` — use the `IconType.Outline` enum, not string literals
- Tag colors: `Color="@("blue")"` not `Color="blue"` — Razor parses bare strings as C# identifiers and produces CS0103
- **`<Text>` inside `@if` blocks cannot have attributes** (RZ1023 error) — use `<span>` instead when the element is conditional

## Authentication and authorization caveats

### `AuthorizeView` context shadowing in table `ActionColumn`
AntBlazor table `ActionColumn` templates bind the row item to a parameter called `context`. `AuthorizeView` also defaults to `context` for its auth state. Nesting `<AuthorizeView>` inside `<ActionColumn>` produces a compile error. Fix: always add `Context="auth"` to `<AuthorizeView>` inside any `ActionColumn`:
```razor
<ActionColumn>
    <AuthorizeView Policy="@AuthPolicy.WorkspaceManage" Context="auth">
        <Button OnClick="() => Delete(context)">Delete</Button>
    </AuthorizeView>
</ActionColumn>
```

### `ItemTemplate` inside `AuthorizeView`
If a `<Select>` with an `<ItemTemplate>` is a descendant of an `<AuthorizeView>`, the same `context` conflict arises. Add `Context="poolItem"` (or any other name) to `<ItemTemplate>`:
```razor
<AuthorizeView Policy="@AuthPolicy.NodePoolOperate">
    <Select ...>
        <ItemTemplate Context="poolItem">
            @{ var item = (MyDto)poolItem!; }
        </ItemTemplate>
    </Select>
</AuthorizeView>
```

### `AuthorizeView` only gates rendering, not code execution
`AuthorizeView` hides UI elements but does not prevent `OnInitializedAsync` or other lifecycle code from running. If a page loads data that requires a specific policy (e.g., fetching interactive pools), gate the code-behind as well:
```csharp
[CascadingParameter] private Task<AuthenticationState>? AuthenticationStateTask { get; set; }
[Inject] private IAuthorizationService AuthorizationService { get; set; } = default!;

protected override async Task OnInitializedAsync()
{
    var authState = await AuthenticationStateTask!;
    var result = await AuthorizationService.AuthorizeAsync(authState.User, null, AuthPolicy.NodePoolOperate);
    if (result.Succeeded)
    {
        _pools = await InteractivePoolService.GetInteractivePoolsAsync();
    }
}
```

### Always serialize all claims for WASM
`AddAuthenticationStateSerialization(options => options.SerializeAllClaims = true)` must be set on the server. Without it, custom `ClaimTypes.Role` claims added by `AppClaimsTransformation` are stripped before the auth state is handed to the WASM client, making all `AuthorizeView` and `[Authorize(Policy = ...)]` directives evaluate as unauthenticated.

### `AppClaimsTransformation` — use `AsNoTracking()`
`AppClaimsTransformation` runs inside the same scoped `DatatealDbContext` as the rest of the request. Always query with `.AsNoTracking()` here. If the user entity is tracked, subsequent repository operations in the same request may find a stale cached instance in the EF identity map, causing silent data corruption or `DbUpdateConcurrencyException`.

### Updating `UserCatalogAccess` — bypass the change tracker
Replacing a user's catalog access list must use `ExecuteDeleteAsync` (bulk SQL) rather than `Clear()` + `AddRange()` on the navigation collection. The EF change tracker cannot reliably reconcile the collection when the entity was previously loaded (even as `AsNoTracking`) in the same DI scope. The transaction must also be wrapped in `db.Database.CreateExecutionStrategy().ExecuteAsync(...)` because Npgsql's `NpgsqlRetryingExecutionStrategy` forbids user-initiated transactions outside a retriable unit.

### Policy names vs role names
`[Authorize(Policy = AuthPolicy.WorkspaceRead)]` and `AuthorizeView Policy="@AuthPolicy.WorkspaceRead"` check **policies** (which map multiple roles). Do not pass a role name where a policy name is expected — the authorization system will look for a policy named `"WorkspaceContributor"` and throw `InvalidOperationException: The AuthorizationPolicy named '...' was not found.`

### OrchestratorProxy — no silent fallthrough
`GetRequiredPolicy` in `OrchestratorProxy.cs` throws `InvalidOperationException` for any path it does not recognize. When adding a new orchestrator endpoint that the client will call via `/api/orchestrator/`, add a matching branch to `GetRequiredPolicy` first. There is no default fallback policy.

## Monaco editor (`CodeCell`)

`CodeCell.razor` wraps BlazorMonaco. `Height=0` = auto-size from line count; `Height>0` = fixed pixel height. `AutomaticLayout=true` causes Monaco to reflow when its container resizes — important after a splitter drag.

**SPA navigation layout fix**: `CodeCell.OnEditorInitAsync` always calls `relayoutMonacoEditor` after editor init. This forces `editor.layout()` on the next `requestAnimationFrame`, ensuring Monaco re-measures its container after the browser's layout pass has settled. Without this, Monaco measures its flexbox container before the browser has computed flex heights on SPA navigation (where the DOM is patched incrementally), resulting in zero-height editors. This call is intentional and must be kept — do not remove it. Any new page that embeds `CodeCell` inherits the fix automatically.

JS is split across focused files in `Datateal.Ui.Server/wwwroot/js/`, all loaded as global scripts in `App.razor`:

| File | Contents | Primary consumers |
|---|---|---|
| `theme.js` | `datatealMonacoReady` promise, `setDatatealTheme`, `getStoredDatatealTheme`, `getDatatealMonacoTheme` | `ThemeService` |
| `monaco-interop.js` | `applyDatatealMonacoTheme`, `setMonacoEditorLanguage`, `getMonacoEditorSelection`, `registerMonacoExecuteCommand`, `relayoutMonacoEditor` | `CodeCell.razor` |
| `semantic-tokens.js` | `registerSemanticTokensCell`, `unregisterSemanticTokensCell`, token registry | `KernelCodeCell.razor` |
| `splitter.js` | `initQueryPageSplitter`, `initCatalogPanelSplitter` | `QueryPage.razor`, `CatalogSidePanel.razor`, `CatalogsPage.razor` |
| `file-helpers.js` | `openFileAsText`, `downloadFile`, `downloadFileBytes`, `clickElement` | `NotebookPage.razor`, `WorkspacePage.razor`, `DataFrameView.razor` |
| `notebook-interop.js` | `scrollNotebookToCell`, `getItemNodePref`, `setItemNodePref` | `NotebookProgressGutter.razor`, `NotebookPage.razor`, `QueryPage.razor` |

**Script load order matters**: `theme.js` must load before `monaco-interop.js` because `applyDatatealMonacoTheme` awaits `window.datatealMonacoReady` which is defined in `theme.js`.

## DataFrameView

`DataFrameView.razor` renders kernel DataFrame/DuckDB results as a virtualized table. Parameter `Fill` (default `false`): when `false`, the component constrains its own height with `overflow: auto; max-height: 400px` (suitable for notebook cells); when `true`, it disables those constraints so the parent container is the only scroll container (use in `QueryPage` results panel).

## Theme

`IThemeService` / `ThemeService` persist the theme preference (`Auto`/`Light`/`Dark`) in `localStorage` as `datateal-theme`. A FOUC-prevention script in `App.razor`'s `<head>` applies the theme synchronously before page paint.
