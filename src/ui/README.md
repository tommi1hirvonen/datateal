# Datateal UI

Blazor Web App: an ASP.NET Core server host (`Datateal.Ui.Server`) with a WebAssembly client (`Datateal.Ui.Client`). Provides the notebook editor, SQL query editor, workspace browser, catalog management, and interactive node pool management.

---

## Project Structure

| Project                             | Role                                                               |
| ----------------------------------- | ------------------------------------------------------------------ |
| `Datateal.Ui.Shared`                | DTOs shared between server and WASM client                         |
| `Datateal.Ui.Server`                | ASP.NET Core host; REST API controllers; EF Core migrations        |
| `Datateal.Ui.Server.Core`           | Domain entities and repository interfaces                          |
| `Datateal.Ui.Server.Application`    | Use-case layer: custom mediator pattern (commands + queries)       |
| `Datateal.Ui.Server.Infrastructure` | EF Core + SQLite; concrete repositories and services               |
| `Datateal.Ui.Client`                | WASM client: pages and typed `HttpClient` services                 |
| `Datateal.Ui.Client.Components`     | Razor Class Library: `CodeCell` (Monaco wrapper), `ExecutionTimer` |

---

## DuckLake Catalogs

DuckLake catalogs are instances of the [DuckLake](https://ducklake.select/) open table format backed by a Postgres metadata store and a parquet data store (local path or Azure Data Lake Storage). The UI allows users to create, browse, and manage catalogs in the `/catalogs` page.

### Managed vs External catalogs

**Managed** catalogs use a shared Postgres instance and base data path configured in `appsettings`. Their connection details are not stored in the database — they are populated at runtime from `CatalogSettings`. This means Postgres credentials and the data path can be updated by changing appsettings without touching the database.

**External** (unmanaged) catalogs have all connection details filled in by the user at creation time. Their details (including encrypted password and connection string) are stored in the database.

### Object explorer

The workspace sidebar shows a catalog tree. Catalogs expand to show schemas; schemas expand to show type folders (Tables, Views, Functions, etc.); folders expand to show individual objects; tables and views expand to show columns and their types.

### Automatic catalog attachment

When a notebook or SQL query is opened, the user can select catalogs to attach to the kernel session. The UI generates a Python setup script that:

1. Installs and loads the `ducklake` extension (and the `azure` extension when needed).
2. Creates DuckDB secrets for the Postgres catalog store and Azure storage.
3. Executes `ATTACH 'ducklake:postgres:' AS catalog_name (DATA_PATH '...')`.

All SQL is sent to the kernel as Python using `duckdb.execute("...")`.

### Configuration

| Key                                | Default                         | Description                                                                                                                                |
| ---------------------------------- | ------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------ |
| `Catalogs:BaseDataPath`            | _(none)_                        | Base path for managed catalog parquet data. Final path = `BaseDataPath/catalogName`. Can be a local path or `abfss://` for Azure Data Lake |
| `Catalogs:StorageConnectionString` | _(none)_                        | Azure Data Lake connection string. Required when `BaseDataPath` uses `az://` or `abfss://`                                                 |
| `Catalogs:CatalogHost`             | `localhost`                     | Postgres host for **direct connections** from the UI server process (admin ops: CREATE DATABASE, metadata queries)                         |
| `Catalogs:CatalogPodHost`          | _(falls back to `CatalogHost`)_ | Postgres host as seen **from inside kernel pods**. Set this when the address differs from `CatalogHost`                                    |
| `Catalogs:CatalogPort`             | `5432`                          | Postgres port                                                                                                                              |
| `Catalogs:CatalogUser`             | _(none)_                        | Postgres user                                                                                                                              |
| `Catalogs:CatalogPassword`         | _(none)_                        | Postgres password                                                                                                                          |

### Local development setup

During local development the UI server runs on the developer machine and can reach Postgres via `localhost`. Kernel pods run in Docker Desktop Kubernetes and must use `host.docker.internal` instead. Configure both addresses:

```json
// appsettings.Development.json
"Catalogs": {
  "BaseDataPath": "/data/ducklake",
  "CatalogHost": "localhost",
  "CatalogPodHost": "host.docker.internal",
  "CatalogPort": 5432,
  "CatalogUser": "postgres",
  "CatalogPassword": "..."
}
```

`BaseDataPath` here matches the `DataVolumeMountPath` set in the Control Plane configuration so that parquet files survive pod restarts. See the [Control Plane README](../control-plane/README.md#persistent-data-volume-local-development) for instructions on mounting a host directory into pods.

---

## Authentication and Authorization

### Overview

Authentication is handled by an external identity provider (currently Entra ID) via OpenID Connect. Authorization is entirely app-managed: roles are stored in the application database and injected as claims after the OIDC token is validated. The two concerns are fully separated — the IdP only proves identity; the app decides what the user can do.

### Authentication flow

#### Entra ID provider (`Authentication:Provider: "EntraId"`)

1. The user visits a protected page and is redirected to the OIDC login endpoint (`/authentication/login`).
2. Entra ID validates credentials and returns an ID token. The server exchanges it for a cookie session (OpenID Connect → Cookie scheme).
3. `AppClaimsTransformation` runs on every authenticated request. It looks up the user in the app database by Entra OID (`objectidentifier` claim) or email (`preferred_username`), then adds `ClaimTypes.Role` claims for each role stored in `AppUser.Roles`.
4. The serialized claims principal (including role claims) is embedded in the server-rendered HTML via `AddAuthenticationStateSerialization(options => options.SerializeAllClaims = true)`. The WASM client reads this payload through `PersistentAuthenticationStateProvider` — no extra auth round-trip is needed from the browser.
5. Logout calls `SignOutAsync` for both the Cookie and OpenID Connect schemes.

#### Dev provider (`Authentication:Provider: "Dev"`)

Every request is automatically authenticated as the user configured in `Authentication:Dev`. No browser redirect or credential prompt occurs.

1. `DevAuthenticationHandler` returns `AuthenticateResult.Success` on every request, building a `ClaimsIdentity` with `preferred_username` and `name` from `Authentication:Dev:User`.
2. **Role resolution depends on whether `Authentication:Dev:Roles` is present in config:**
   - **`Roles` is set** (e.g. `["Admin"]` or even `[]`): those roles are applied as-is, a `datateal:dev-roles-override` marker claim is added, and `AppClaimsTransformation` skips its admin seed list check and database lookup entirely. The configured roles are the only roles.
   - **`Roles` is absent / null**: no roles are added by the handler. `AppClaimsTransformation` runs normally — it looks up the user in the database by `preferred_username` (email) and applies their stored roles and catalog permissions, exactly as a real OIDC login would. This lets you impersonate any database user by setting their email and omitting `Roles`.
3. Auth state is serialized and sent to the WASM client identically to the Entra ID flow.
4. Login redirects straight to the return URL (the user is already authenticated). Logout is a no-op redirect to `/`.

### Pluggable identity provider

The OIDC/auth setup is isolated behind `IIdentityProviderSetup` (`Datateal.Auth.Abstractions`). The active provider is selected at startup by reading `Authentication:Provider` from configuration. Each identity provider ships its own implementation:

| Package                 | Implementation                 | Extension method             | `Authentication:Provider` value |
| ----------------------- | ------------------------------ | ---------------------------- | ------------------------------- |
| `Datateal.Auth.EntraId` | `EntraIdIdentityProviderSetup` | `AddEntraIdAuthentication()` | `"EntraId"` (default)           |
| `Datateal.Auth.Dev`     | `DevIdentityProviderSetup`     | `AddDevAuthentication()`     | `"Dev"`                         |

Set `Authentication:Provider` in `appsettings.Development.json` to switch providers. Only one provider is active at a time.

Providers may also implement `ILoginLogoutEndpoints` (from `Datateal.Auth`) to customize the `/authentication/login` and `/authentication/logout` endpoints. `DevIdentityProviderSetup` implements this interface; `EntraIdIdentityProviderSetup` does not, so the default OIDC challenge/sign-out behavior is preserved for Entra ID.

### Admin seed list

Users listed in `appsettings` under `Authorization:AdminUsers` receive the `Admin` role unconditionally, without requiring a database record. This allows bootstrapping access before any users have been added through the UI.

```json
// appsettings.json (or appsettings.Development.json)
"Authorization": {
  "AdminUsers": [ "admin@example.com" ]
}
```

### User management

Users are stored in `AppUser` (Postgres via EF Core). Key fields:

| Field                 | Description                                                                                                                      |
| --------------------- | -------------------------------------------------------------------------------------------------------------------------------- |
| `Email`               | Primary identifier used for admin seed matching and first-login lookup                                                           |
| `ExternalId`          | Entra OID — captured on first login for stable future lookups. Once set it becomes the primary key for `AppClaimsTransformation` |
| `IsEnabled`           | Disabled users are authenticated but receive no role claims                                                                      |
| `Roles`               | `List<string>` stored as a JSON array column; use `DatatealRole.*` constants                                                     |
| `HasAllCatalogAccess` | `true` = user can access all catalogs (present and future)                                                                       |
| `CatalogAccessList`   | `UserCatalogAccess` rows granting access to specific catalogs when `HasAllCatalogAccess` is `false`                              |

Admin and `CatalogContributor` users always have implicit access to all catalogs regardless of `HasAllCatalogAccess`.

### Roles and policies

Roles are coarse-grained capability buckets. Policies are the named groups checked by `[Authorize]` and `<AuthorizeView>` — always use policy names, not role names directly.

#### Roles (`DatatealRole`)

Roles are either **tenant-global** (stored on `AppUser.Roles`, assigned on the Users page, effective across the whole instance) or **per-workspace** (stored on `WorkspaceMembership.Roles`, assigned from a workspace's members list, effective only within that workspace).

| Role                   | Scope         | What it grants                                                                                                                                 |
| ---------------------- | ------------- | ---------------------------------------------------------------------------------------------------------------------------------------------- |
| `Admin`                | Tenant-global | Full access across the instance: manage users and every workspace, and access all catalogs. Implicitly has every role in every workspace       |
| `CatalogContributor`   | Tenant-global | Create/edit/delete catalogs and schemas across the tenant; implicit access to all catalog data                                                 |
| `WorkspaceAdmin`       | Per-workspace | Administer a single workspace: manage its members/roles and do everything the other per-workspace roles allow within it. No tenant-level admin |
| `WorkspaceContributor` | Per-workspace | Create/edit/delete folders, notebooks, queries in the workspace. Needs `NodePoolOperator` to connect to kernels and execute code               |
| `WorkspaceReader`      | Per-workspace | Read notebooks and queries in the workspace; no create/edit/delete. Needs `NodePoolOperator` to connect to kernels and execute code            |
| `NodePoolContributor`  | Per-workspace | Create/edit/delete node pool configs in the workspace; start/stop nodes; create kernel sessions and execute code                               |
| `NodePoolOperator`     | Per-workspace | Start/stop interactive node pools in the workspace; create kernel sessions and execute code (no config changes)                                |
| `JobContributor`       | Per-workspace | Create/edit/delete jobs, tasks, params, schedules in the workspace                                                                             |
| `JobOperator`          | Per-workspace | Run, monitor, and cancel jobs in the workspace (no config changes)                                                                             |
| `JobReader`            | Per-workspace | Read and monitor jobs and runs in the workspace; no run or cancel                                                                              |
| `EnvironmentManager`   | Per-workspace | Manage the workspace's environment variables, secrets, and packages                                                                            |

#### Policies (`AuthPolicy`)

| Policy              | Roles included                                |
| ------------------- | --------------------------------------------- |
| `Admin`             | Admin                                         |
| `NodePoolManage`    | Admin, NodePoolContributor                    |
| `NodePoolOperate`   | Admin, NodePoolContributor, NodePoolOperator  |
| `JobManage`         | Admin, JobContributor                         |
| `JobOperate`        | Admin, JobContributor, JobOperator            |
| `JobRead`           | Admin, JobContributor, JobOperator, JobReader |
| `WorkspaceManage`   | Admin, WorkspaceContributor                   |
| `WorkspaceRead`     | Admin, WorkspaceContributor, WorkspaceReader  |
| `CatalogManage`     | Admin, CatalogContributor                     |
| `EnvironmentManage` | Admin, EnvironmentManager                     |

### Controller authorization

Each controller declares a class-level `[Authorize(Policy = ...)]`. Mutating endpoints within a controller may escalate to a stricter policy:

| Controller                   | Class policy                | Override for mutating endpoints                       |
| ---------------------------- | --------------------------- | ----------------------------------------------------- |
| `WorkspaceController`        | `WorkspaceRead`             | `WorkspaceManage` for create/rename/move/delete/clone |
| `CatalogsController`         | `Authorize` (authenticated) | `CatalogManage` for create/update/delete              |
| `KernelsController`          | `NodePoolOperate`           | —                                                     |
| `InteractivePoolsController` | `NodePoolOperate`           | —                                                     |
| `NodesController`            | `NodePoolOperate`           | —                                                     |
| `EnvironmentController`      | `EnvironmentManage`         | —                                                     |
| `UsersController`            | `Admin`                     | —                                                     |

### Orchestrator proxy authorization

Client calls to `/api/orchestrator/{**path}` are routed through `OrchestratorProxy`. The proxy enforces authorization before forwarding:

| Path prefix                    | GET               | POST `/trigger` or `/cancel` | Other write methods |
| ------------------------------ | ----------------- | ---------------------------- | ------------------- |
| `node-pools/`                  | `NodePoolOperate` | —                            | `NodePoolManage`    |
| `jobs/`                        | `JobRead`         | `JobOperate` (trigger)       | `JobManage`         |
| `runs/`                        | `JobRead`         | `JobOperate` (cancel)        | `JobManage`         |
| `admin/` (except `/timezones`) | `Admin`           | `Admin`                      | `Admin`             |
| `admin/timezones`              | none              | —                            | —                   |

Any path not handled by `GetRequiredPolicy` throws `InvalidOperationException` (no silent fallthrough).

### Service-to-service authentication

Backend services authenticate each other with API keys, not user tokens:

- **UI → Orchestrator**: the `Orchestrator` named `HttpClient` injects an `Authorization: ApiKey <key>` header via `ApiKeyDelegatingHandler`. Key configured at `ServiceAuth:Orchestrator:ApiKey`.
- **UI → Control Plane** (when applicable): same pattern with its own key.
- **Orchestrator → Control Plane**: same pattern.
- Backend services validate incoming keys via `ApiKeyAuthenticationHandler` reading `ServiceAuth:ExpectedApiKey`.

### Configuration reference

| Key                                   | Provider  | Description                                                                                                                                                                                                       |
| ------------------------------------- | --------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Authentication:Provider`             | both      | `"EntraId"` (default) or `"Dev"`                                                                                                                                                                                  |
| `Authorization:AdminUsers`            | both      | Email list of bootstrap admin users (Entra ID: matched against `preferred_username`; Dev: matched against `Authentication:Dev:User:Email`)                                                                        |
| `Authentication:EntraId:TenantId`     | `EntraId` | Entra ID tenant                                                                                                                                                                                                   |
| `Authentication:EntraId:ClientId`     | `EntraId` | Entra ID app registration client ID                                                                                                                                                                               |
| `Authentication:EntraId:ClientSecret` | `EntraId` | Entra ID client secret (use secrets / env var in production)                                                                                                                                                      |
| `Authentication:Dev:User:Email`       | `Dev`     | Email of the auto-authenticated dummy user (`dev@local` default)                                                                                                                                                  |
| `Authentication:Dev:User:DisplayName` | `Dev`     | Display name of the dummy user                                                                                                                                                                                    |
| `Authentication:Dev:Roles`            | `Dev`     | JSON array of role names granted to every request. Use `DatatealRole` constants. Example: `["Admin"]`. **When omitted**, roles are looked up from the database by `User:Email` instead (same as real OIDC login). |
| `ServiceAuth:Orchestrator:ApiKey`     | both      | API key the UI uses when calling the orchestrator                                                                                                                                                                 |

---

## Pages

| Page                  | Route                         | Policy            | Description                                                                                                        |
| --------------------- | ----------------------------- | ----------------- | ------------------------------------------------------------------------------------------------------------------ |
| `Home.razor`          | `/`                           | —                 | Welcome page                                                                                                       |
| `WorkspacePage.razor` | `/workspace`                  | `WorkspaceRead`   | Workspace browser: create, rename, move, clone, delete notebooks and queries                                       |
| `NotebookPage.razor`  | `/notebook`, `/notebook/{id}` | `WorkspaceRead`   | Polyglot notebook editor; kernel toolbar requires `NodePoolOperate`                                                |
| `QueryPage.razor`     | `/query`, `/query/{id}`       | `WorkspaceRead`   | SQL editor with results panel; kernel toolbar requires `NodePoolOperate`                                           |
| `NodePoolsPage.razor` | `/node-pools`                 | `NodePoolOperate` | Node pool config management; Interactive and Job pool tabs; Active nodes tab; edit/delete require `NodePoolManage` |
| `Kernels.razor`       | `/nodes/{name}/kernels`       | `NodePoolOperate` | Kernel management per node                                                                                         |
| `CatalogsPage.razor`  | `/catalogs`                   | Authenticated     | DuckLake catalog management; create/edit/delete require `CatalogManage`                                            |
| `JobsPage.razor`      | `/jobs`                       | `JobRead`         | Job list; create/delete require `JobManage`, run requires `JobOperate`                                             |
| `JobEditorPage.razor` | `/jobs/{id}`                  | `JobRead`         | Job editor; save/task/param/schedule edits require `JobManage`, run requires `JobOperate`                          |
| `JobRunPage.razor`    | `/runs/{id}`                  | `JobRead`         | Job run detail; cancel requires `JobOperate`                                                                       |
| `UsersPage.razor`     | `/users`                      | `Admin`           | User management (create, edit roles, catalog access)                                                               |
| `Settings.razor`      | `/settings`                   | —                 | Theme settings                                                                                                     |

---

## Styling

Custom CSS in `Datateal.Ui.Server/wwwroot/css/app.css`. Component-scoped CSS in `Datateal.Ui.Client.Components/wwwroot/defaults.css`. Dark mode class `ant-dark` is toggled on `<html>`.

Do not use `var(--ant-*)` CSS custom properties — they do not exist in this build of Ant Design Blazor. Use hardcoded hex values and target dark mode with `html.ant-dark` selectors in `app.css`. Typical values: borders `#d9d9d9` / `#434343` (dark), subtle backgrounds `#fafafa` / `#1d1d1d` (dark).
