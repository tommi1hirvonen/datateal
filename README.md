# Datateal

**Datateal** is an open lakehouse analytics platform built entirely on open-source technology. It gives data teams a self-hosted, cloud-agnostic environment for interactive data exploration and automated data pipelines — powered by [DuckDB](https://duckdb.org/) and the [DuckLake](https://ducklake.select/) open table format.

---

## Why Datateal?

Modern cloud data warehouses are expensive, proprietary, and tightly coupled to a single vendor. Datateal takes a different approach:

- **Open formats, open stack.** DuckDB's columnar engine and the DuckLake open table format mean your data is never locked in. Parquet files on any S3-compatible object store are your warehouse.
- **Single-node performance that scales.** DuckDB delivers remarkable analytical throughput on a single VM — no cluster required for most workloads. Scale up the VM size, not the architecture.
- **Cloud-agnostic by design.** The control plane runs on Kubernetes and provisions compute nodes on demand. Any cloud provider that offers a managed Kubernetes service and an S3-compatible store works out of the box. Azure AKS + Azure Data Lake is the reference implementation, but the abstraction layer makes it straightforward to target other providers.
- **Cost efficiency.** Compute nodes spin up for interactive sessions and job runs, then shut down automatically after a configurable idle period. You pay for compute only when it is actually running queries.

---

## Architecture

```
┌─────────────────────────────────────────────────────┐
│  Browser (Blazor WASM)                              │
│  Notebook editor · SQL editor · Workspace · Jobs    │
└────────────────────────┬────────────────────────────┘
                         │ HTTP / REST
                         ▼
┌─────────────────────────────────────────────────────┐
│  UI Server  (ASP.NET Core)                          │
│  Auth · Workspace · Catalogs · Node pool management │
└───────┬─────────────────────────┬───────────────────┘
        │                         │
        ▼                         ▼
┌───────────────┐       ┌─────────────────────────────┐
│  Orchestrator │       │  Control Plane              │
│  (ASP.NET)    │──────▶│  (ASP.NET Core)             │
│  Jobs · DAGs  │       │  Provisions nodes & kernels │
│  Scheduling   │       │  K8s API proxy tunnel       │
└───────────────┘       └────────────┬────────────────┘
                                     │ K8s API + HTTP proxy
                                     ▼
                        ┌─────────────────────────────┐
                        │  Runtime pods               │
                        │  FastAPI · Jupyter kernels  │
                        │  DuckDB · Python            │
                        └─────────────────────────────┘
```

The **Control Plane** provisions compute nodes (AKS agent pools or local Kubernetes pods) on demand and tunnels all kernel API traffic through the Kubernetes API server's built-in HTTP proxy — no public IPs, load balancers, or VNet peering required. The **Runtime** is a FastAPI service running inside each pod that manages Jupyter kernels in an isolated Python environment; the kernel venv contains DuckDB and any user-supplied packages. The **Orchestrator** evaluates job DAGs, drives retries, and handles crash recovery independently of the UI. All backend services share a single PostgreSQL database and communicate via internal API keys.

---

## Tech Stack

| Layer                   | Technology                                                    |
| ----------------------- | ------------------------------------------------------------- |
| Frontend                | Blazor WebAssembly + ASP.NET Core (Blazor Web App)            |
| UI components           | Ant Design Blazor                                             |
| Code editor             | Monaco Editor (BlazorMonaco)                                  |
| Backend services        | ASP.NET Core 10 (.NET Aspire)                                 |
| Job scheduling          | Quartz.NET                                                    |
| Query engine            | DuckDB                                                        |
| Table format            | DuckLake (open table format over Parquet + Postgres metadata) |
| Runtime API             | FastAPI (Python)                                              |
| Kernel execution        | Jupyter kernels (`ipykernel`)                                 |
| Code intelligence       | Jedi (completions), pyflakes (diagnostics)                    |
| Container orchestration | Kubernetes (Docker Desktop locally, AKS in production)        |
| Infrastructure as code  | Bicep                                                         |
| Primary database        | PostgreSQL (EF Core)                                          |
| Authentication          | Entra ID via OpenID Connect (pluggable)                       |

---

## Features

### Polyglot Notebooks

Notebooks support three cell types that can be freely mixed:

- **Python** — full Python execution in an isolated Jupyter kernel
- **SQL** — DuckDB SQL; cells are transparently wrapped as `duckdb.sql("""...""")` before execution
- **Markdown** — rich documentation rendered inline

Every code cell uses the **Monaco editor** — the same editor that powers VS Code — with syntax highlighting for Python and SQL.

**Linting and diagnostics** run as you type. Python cells are checked with `pyflakes` for import errors, undefined names, and unused variables. Syntax errors are caught by the Python compiler before the cell is even sent to the kernel. Diagnostics appear inline as squiggly underlines with hover messages, and the gutter shows error markers.

**Jedi-powered completions** are context-aware: the runtime collects all prior cell code as context, so variables and imports defined in earlier cells are visible in auto-complete suggestions for later cells.

```python
# Cell 1 — Python
import duckdb
import pandas as pd

orders = pd.read_parquet("orders.parquet")
```

```sql
-- Cell 2 — SQL: query the pandas DataFrame from Cell 1 directly
SELECT
    customer_id,
    SUM(amount) AS total
FROM orders
GROUP BY customer_id
ORDER BY total DESC
LIMIT 10
```

#### DuckDB Replacement Scans

One of DuckDB's most powerful features is its **replacement scan**: any Python variable in scope that is a `pandas.DataFrame` or a `duckdb.DuckDBPyRelation` can be queried by name directly in SQL. The result of the **most recent SQL cell** is also available as `_sqldf` in the next cell — no explicit variable assignment needed.

```python
# Cell 1 — Python
import pandas as pd
df = pd.read_csv("sales.csv")
```

```sql
-- Cell 2 — SQL: query the DataFrame by its Python variable name
SELECT region, SUM(revenue) FROM df GROUP BY region
```

```sql
-- Cell 3 — SQL: query the result of Cell 2 using _sqldf
SELECT * FROM _sqldf WHERE SUM_revenue > 100000
```

---

### SQL Query Editor

The standalone SQL editor provides a split-screen layout with a resizable SQL editor pane on top and a results panel below. Results are displayed in a virtualized table that handles large result sets efficiently. The editor has the same Monaco-based syntax highlighting and connects to the same compute node infrastructure as notebooks.

---

### Workspace

Notebooks and SQL queries are organized in a hierarchical folder structure. The workspace browser supports:

- Create, rename, move, clone, and delete notebooks, queries, and folders
- A sidebar **catalog object explorer** that shows all attached DuckLake catalogs expanded to schemas, tables, views, and columns

---

### Node Pools

Datateal separates compute into two pool types:

#### Interactive Node Pools

Interactive pools have at most one live compute node at a time. When a user opens a notebook or SQL editor and selects an interactive pool, the node is started on demand if not already running. The node stays alive for a configurable idle period after the last kernel activity, then is automatically evicted. This is ideal for exploratory work where you want a persistent session across multiple notebooks without paying for idle time during long breaks.

#### Job Node Pools

Job pools provision fresh nodes for each job run and delete them when the run completes. They support:

- **Warm standby nodes** — pre-provisioned standby nodes that are instantly claimed when a job starts, eliminating cold-start wait time. A `WarmNodes` count keeps a target number of ready nodes available at all times; they are automatically replenished after being claimed.
- **`MaxNodes` cap** — limits the total number of live nodes (warm + active) for a pool across all concurrent runs. Jobs that would exceed the cap wait until a node becomes available, with a configurable `NodeAcquireTimeout`.

Both pool types support custom **VM sizes**, descriptions, and idle timeout configuration.

---

### Environment: Secrets, Variables, and Wheel Packages

Each node pool can be configured with:

- **Environment secrets** — sensitive key-value pairs (API keys, credentials, connection strings) stored encrypted and injected as Kubernetes `Secret` references into the pod environment. User code can access them via `os.environ` just like any other environment variable.
- **Environment variables** — non-sensitive key-value pairs mounted as plain environment variables.
- **Wheel packages** — `.whl` files uploaded through the UI and delivered to kernel pods as Kubernetes `ConfigMap` volumes, installed into the kernel's Python environment at pod startup. This allows distributing internal Python packages to compute nodes without rebuilding the container image.

Additional packages can also be specified as a space-separated `KERNEL_PACKAGES` list or a mounted `requirements.txt` file, installed at pod startup without requiring an image rebuild.

---

### DuckLake Catalogs

Datateal uses the **DuckLake** open table format: metadata is stored in PostgreSQL and data files are Parquet on any object store. Multiple catalogs can be attached to a session simultaneously.

- **Managed catalogs** share the platform's Postgres instance and a configured base data path (local or `abfss://` Azure Data Lake). Connection details are not stored in the database — they are resolved at runtime from `appsettings`, so credentials can be rotated without touching data.
- **External catalogs** have user-supplied connection details (Postgres host, credentials, data path) stored in the database. Use these to connect to independently managed DuckLake catalogs.

When opening a notebook or SQL editor, users select which catalogs to attach. The UI generates the Python setup code that installs and loads the `ducklake` DuckDB extension, creates the required DuckDB secrets, and executes `ATTACH 'ducklake:postgres:' AS catalog_name (DATA_PATH '...')`. The catalog is then immediately queryable by name in any SQL cell.

```sql
-- Query a table in an attached DuckLake catalog
SELECT * FROM my_catalog.main.sales LIMIT 100;

-- Create a new table directly from a DataFrame
CREATE TABLE my_catalog.main.enriched_sales AS
SELECT * FROM _sqldf;
```

---

### Jobs

Jobs are defined as **DAGs of typed tasks** and are the primary way to run automated data pipelines.

#### Task Types

| Type           | Description                                                  |
| -------------- | ------------------------------------------------------------ |
| `NotebookTask` | Executes a workspace notebook on a specified node pool       |
| `SqlQueryTask` | Executes a workspace SQL query file on a specified node pool |
| `SubJobTask`   | Triggers another job and waits for it to complete            |

#### DAG Execution

- Tasks run **in parallel** as soon as their dependencies are satisfied.
- Dependency edges carry conditions: `OnSuccess`, `OnFailure`, `OnCompletion`, or `OnSkip` — allowing conditional branching within the DAG.
- **Skip propagation** is eager: a task whose dependency condition can never be satisfied is automatically skipped, and downstream tasks are re-evaluated immediately.
- **Retries**: per-task `MaxRetries` and `RetryInterval`. Retries reuse the same `TaskRun` record and increment an attempt counter.
- **Timeouts**: optional per-task execution timeout.

#### Parameters

Jobs declare named parameters with optional defaults and a required flag. Parameters are resolved at trigger time (manually or by the scheduler) and injected into task execution in two ways:

- **Template substitution**: `${{ param_name }}` placeholders in task configuration are replaced with the resolved value before execution.
- **Notebook parameterisation**: if the notebook contains a cell tagged `parameters`, an injected-parameters cell is inserted immediately after it before the notebook runs. The injected cell is never written back to the source notebook.

```yaml
# Example: YAML job definition
name: daily-sales-pipeline
parameters:
  - name: run_date
    defaultValue: "2024-01-01"
    isRequired: true

tasks:
  - name: ingest
    type: notebook
    notebookPath: /pipelines/ingest_sales
    nodePool: job-pool-standard

  - name: transform
    type: notebook
    notebookPath: /pipelines/transform_sales
    nodePool: job-pool-standard
    dependencies:
      - task: ingest
        condition: OnSuccess
```

#### Scheduling

Jobs can have one or more **cron schedules** with:

- 6-field Quartz cron expressions (`seconds minutes hours day-of-month month day-of-week`)
- Per-schedule timezone with automatic DST handling
- Per-schedule parameter overrides (e.g., run the same job with different parameters on different schedules)
- Schedule changes take effect **immediately** without restarting the service

```
# Examples
0 0 6 * * ?         # Every day at 06:00:00
0 30 8 ? * MON-FRI  # Weekdays at 08:30:00
0 0 */4 * * ?       # Every 4 hours
```

#### Additional Job Features

- **Job snapshot**: the complete job definition is snapshotted as JSON at trigger time. Editing a job never affects a run already in progress.
- **Run cancellation**: cancel a running job via the UI; cancellation propagates through the entire DAG.
- **Concurrent run cap**: per-job `MaxConcurrentRuns` limit enforced at trigger time.
- **Crash recovery**: the orchestrator re-dispatches any `Running` or `Pending` runs on startup — interrupted runs resume from where they stopped.
- **History retention**: old run records are automatically purged on a configurable schedule.
- **YAML import/export**: jobs can be exported to and imported from a YAML format covering tasks, parameters, schedules, and node pool definitions. Workspace paths are resolved to stable IDs at import time, so notebooks and queries can be safely renamed or moved later.
- **`%run` magic**: Python notebook cells can use `%run path/to/other/notebook` to inline another notebook's content, with recursive resolution and cycle detection.

---

### AI Coding Assistant

An AI assistant panel is built directly into the notebook and SQL query editors. It understands the full context of what you are working on — the complete notebook content, the currently focused cell, the attached catalog schemas, and the available wheel packages — so you can ask questions and get answers that are grounded in your actual data model.

#### Chat Mode

Ask questions about your code, get explanations, request rewrites, or have the assistant generate new cells. Responses stream token-by-token and are rendered with syntax-highlighted code blocks. Any code block in the AI's response has an **Apply** button that inserts or replaces the content of the focused cell with one click.

```
You:  Write a SQL query that joins the orders and customers tables
      from the sales catalog and calculates monthly revenue per region.

AI:   Here's a query that joins the two tables and aggregates by month and region:

      [Apply]
      sql
      SELECT
          DATE_TRUNC('month', o.order_date) AS month,
          c.region,
          SUM(o.amount)                     AS revenue
      FROM sales.main.orders o
      JOIN sales.main.customers c ON o.customer_id = c.id
      GROUP BY 1, 2
      ORDER BY 1, 2
```

#### Edit Mode (Agent)

Switch to **Edit mode** and describe the changes you want made to the notebook as a whole. The AI acts as an agent: it calls structured tools (`propose_cell_edit`, `propose_cell_insert`, `propose_cell_remove`) to produce a set of proposed changes, rather than describing them in prose. After the agent completes, the changes appear in a **Proposed Changes panel** where you can:

- Review each proposed edit, insertion, or deletion individually — with an expandable preview of the new cell content and the AI's explanation
- Accept or reject changes individually, or select/deselect all at once
- Apply only the accepted changes to the notebook with a single click

This makes it practical to have the AI author or substantially refactor entire notebooks while keeping you in full control of what actually gets applied.

#### Context Awareness

The system prompt is dynamically assembled for every request. It includes:

- The full schema of every DuckLake catalog attached to the current notebook or query (table names, column names, data types, and any comments)
- The list of available wheel packages installed in the environment
- Datateal-specific instructions (DuckDB SQL dialect, replacement scan syntax, notebook cell conventions)

This means the assistant can reference your actual table and column names, generate syntactically correct DuckDB SQL, and understand the project's Python and SQL conventions without you needing to explain them.

#### Configuration

The AI assistant uses **Azure OpenAI**. Configure the endpoint URL, API key, and deployment name in the Settings page (`/settings`). Credentials are stored in `localStorage` in the browser and are sent directly from the client to the server over SignalR — they are never persisted server-side.

The reference implementation is deployed via **[Microsoft Foundry](https://ai.azure.com/)**, which provides model hosting, deployment management, and access to the latest OpenAI models (e.g. `GPT-4.1-mini`) through a single Azure resource.

---

### Authentication & Authorization

Authentication is delegated to an external OIDC provider (Entra ID by default; pluggable via `IIdentityProviderSetup`). Authorization is fully app-managed with a role-based policy system:

| Role                                           | Capability                         |
| ---------------------------------------------- | ---------------------------------- |
| `Admin`                                        | Full access                        |
| `NodePoolContributor` / `NodePoolOperator`     | Manage or operate compute nodes    |
| `JobContributor` / `JobOperator` / `JobReader` | Manage, trigger, or monitor jobs   |
| `WorkspaceContributor` / `WorkspaceReader`     | Edit or read notebooks and queries |
| `CatalogContributor`                           | Manage catalog definitions         |
| `EnvironmentManager`                           | Manage secrets and variables       |

A bootstrap `AdminUsers` list in `appsettings` lets the first admin log in before any users have been configured in the database.

---

## Infrastructure

The Bicep template in `src/infra/` deploys the full production environment on Azure:

- AKS cluster with a dedicated VNet and subnet for compute node pools
- Azure Container Registry (ACR) — kubelet managed identity has `AcrPull` automatically; no image pull secrets needed
- `disableLocalAccounts: true` on AKS with Azure RBAC — no static kubeconfig credentials
- Two user-assigned managed identities (control plane + kubelet)

For local development, the stack runs on Docker Desktop Kubernetes with `.NET Aspire` orchestrating all services, hot reload, and distributed telemetry.

---

## Repository Structure

```
src/
├── ui/             # Blazor Web App (server + WASM client)
├── control-plane/  # ASP.NET Core: node & kernel provisioning
├── orchestrator/   # ASP.NET Core: job execution engine
├── runtime/        # FastAPI: per-node kernel management (Python)
├── shared/         # Shared domain types (Datateal.Core)
├── app-host/       # .NET Aspire app host
└── infra/          # Bicep infrastructure templates
```
