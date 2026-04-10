---
applyTo: src/ui/**
---

# DuckHouse UI

Blazor Web App: ASP.NET Core server host (`DuckHouse.Ui.Server`) with a WebAssembly client (`DuckHouse.Ui.Client`). Uses **Ant Design Blazor** (`AntDesign`) for UI components, **BlazorMonaco** for code editing, and **Markdig** for Markdown rendering. Integrates with .NET Aspire.

## Projects

| Project | Role |
|---|---|
| `DuckHouse.Ui.Shared` | DTOs shared between server and WASM client (`Nodes/`, `Kernels/`, `Workspace/` subfolders) |
| `DuckHouse.Ui.Server` | ASP.NET Core host; REST API controllers, Blazor/WASM bootstrap, EF Core migrations |
| `DuckHouse.Ui.Server.Core` | Domain layer: entity classes, repository interfaces |
| `DuckHouse.Ui.Server.Application` | Use-case layer: custom mediator pattern (commands + queries) |
| `DuckHouse.Ui.Server.Infrastructure` | EF Core + SQLite; concrete repositories, `AddInfrastructureServices()` |
| `DuckHouse.Ui.Client` | WASM client: pages, layouts, typed `HttpClient` services |
| `DuckHouse.Ui.Client.Components` | Razor Class Library: `CodeCell` (Monaco wrapper), `ExecutionTimer` |

## Architecture

**Server** follows Clean Architecture (Core → Application → Infrastructure → Host). Use cases are implemented via a custom mediator (`IMediator`, `IRequest<T>`, `IRequestHandler<TReq,TRes>`) with commands in `Mediator/Commands/` and queries in `Mediator/Queries/`. REST API controllers in `DuckHouse.Ui.Server/Controllers/` map requests to mediator calls.

**Hosting**: `App.razor` renders `<Routes>` with `InteractiveWebAssemblyRenderMode(prerender: false)`. All interactive pages live in the WASM client. No prerender.

**Client services**: typed `HttpClient` wrappers in `DuckHouse.Ui.Client/Services/` call the server REST API. Register all services in `DuckHouse.Ui.Client/Program.cs`.

## Workspace feature

Workspaces store **notebooks** and **SQL query files** in a folder hierarchy backed by EF Core + SQLite using **TPH inheritance**. `WorkspaceItem` is the abstract EF base class; `Notebook` and `Query` are concrete subclasses. The EF discriminator column is `ItemType varchar(32)`. `Query` adds nullable columns for the last execution result (`LastExecutedAt`, `LastDurationMs`, `LastResultStatus`, `LastResultJson` as a JSON blob).

- Server: `WorkspaceController` at `api/workspace`; typed repository methods use `.OfType<Notebook>()` / `.OfType<Query>()`
- Client: `IWorkspaceService` / `WorkspaceService` in `DuckHouse.Ui.Client/Services/`
- Workspace listing: folders first (alphabetical), then notebooks and queries merged and sorted alphabetically

**WorkspacePage** (`/workspace`): lists folders and items with a Type column; supports create, rename, move, clone, delete for both notebooks and queries.

**QueryPage** (`/query`, `/query/{id}`): split-screen SQL editor (top) + results panel (bottom). A draggable divider uses `initQueryPageSplitter(topPaneId, handleId, dotNetRef)` JS interop; drag is pure JS for smoothness, fires `[JSInvokable] OnSplitterDragEnd(double height)` on release. `QueryPage` implements `IAsyncDisposable` to dispose the `DotNetObjectReference`.

**NotebookPage** (`/notebook`, `/notebook/{id}`): polyglot notebook with Python, SQL, and Markdown cells. SQL cells are wrapped as `import duckdb; duckdb.sql("""...""")` before kernel execution.

## Client pages

| Page | Route | Description |
|---|---|---|
| `Home.razor` | `/` | Welcome page |
| `WorkspacePage.razor` | `/workspace` | Workspace browser |
| `NotebookPage.razor` | `/notebook` | Polyglot notebook editor |
| `QueryPage.razor` | `/query` | SQL query editor with results panel |
| `Nodes.razor` | `/nodes` | Node management |
| `Kernels.razor` | `/nodes/{name}/kernels` | Kernel management per node |
| `KernelSession.razor` | `/nodes/{name}/kernels/{id}` | Interactive kernel REPL |
| `Settings.razor` | `/settings` | Theme settings |

## Styling

Custom CSS in `server/DuckHouse.Ui.Server/wwwroot/css/app.css`. Component-scoped CSS in `DuckHouse.Ui.Client.Components/wwwroot/defaults.css`. Dark mode class `ant-dark` is toggled on `<html>`.

**Do not use `var(--ant-*)` CSS custom properties** — they do not exist in this build of Ant Design Blazor. Use hardcoded hex values and target dark mode with `html.ant-dark` selectors in `app.css`. Typical values: borders `#d9d9d9` / `#434343` (dark), subtle backgrounds `#fafafa` / `#1d1d1d` (dark).

## Ant Design Blazor conventions

- Register with `builder.Services.AddAntDesign()` in both server and client `Program.cs`
- Icons: `<Icon Type="@IconType.Outline.X" />` — use the `IconType.Outline` enum, not string literals
- Tag colors: `Color="@("blue")"` not `Color="blue"` — Razor parses bare strings as C# identifiers and produces CS0103
- **`<Text>` inside `@if` blocks cannot have attributes** (RZ1023 error) — use `<span>` instead when the element is conditional

## Monaco editor (`CodeCell`)

`CodeCell.razor` wraps BlazorMonaco. `Height=0` = auto-size from line count; `Height>0` = fixed pixel height. `AutomaticLayout=true` causes Monaco to reflow when its container resizes — important after a splitter drag. JS helpers in `App.razor`: `setMonacoEditorLanguage`, `getDuckhouseMonacoTheme`, `openFileAsText`, `downloadFile`, `clickElement`, `initQueryPageSplitter`.

## DataFrameView

`DataFrameView.razor` renders kernel DataFrame/DuckDB results as a virtualized table. Parameter `Fill` (default `false`): when `false`, the component constrains its own height with `overflow: auto; max-height: 400px` (suitable for notebook cells); when `true`, it disables those constraints so the parent container is the only scroll container (use in `QueryPage` results panel).

## Theme

`IThemeService` / `ThemeService` persist the theme preference (`Auto`/`Light`/`Dark`) in `localStorage` as `duckhouse-theme`. A FOUC-prevention script in `App.razor`'s `<head>` applies the theme synchronously before page paint.

