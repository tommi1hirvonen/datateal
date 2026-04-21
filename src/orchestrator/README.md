# DuckHouse Orchestrator

ASP.NET Core service that schedules, dispatches, and monitors multi-task jobs against compute nodes managed by the Control Plane. Jobs are defined as DAGs of typed tasks; each run is executed asynchronously with full retry, skip-propagation, and crash-recovery support.

---

## How the Orchestrator Fits in the Solution

```
┌──────────────────────────────────────────────────────────────────────┐
│  DuckHouse UI (browser / API)                                        │
│  • Defines jobs, schedules, and node pool configs                    │
│  • Triggers runs via POST /api/jobs/{id}/trigger                     │
│  • Polls run and task-run status                                     │
└──────────────────────┬───────────────────────────────────────────────┘
                       │ HTTP (REST)
                       ▼
┌──────────────────────────────────────────────────────────────────────┐
│  Orchestrator (this service)                                         │
│  • Persists jobs, runs, and results in the shared PostgreSQL DB      │
│  • Evaluates cron schedules; dispatches runs manually or on schedule │
│  • Drives each run's DAG: provisions nodes, executes tasks, retries  │
└──────────────────────┬───────────────────────────────────────────────┘
                       │ HTTP (REST) via IControlPlaneClient
                       ▼
┌──────────────────────────────────────────────────────────────────────┐
│  Control Plane                                                       │
│  • Provisions / deletes compute nodes (AKS agent pools or local)     │
│  • Creates / deletes Jupyter kernels on those nodes                  │
│  • Runs code cells and returns execution results                     │
└──────────────────────────────────────────────────────────────────────┘
```

The orchestrator never executes code directly. It translates job tasks into kernel execution requests sent through the Control Plane's proxied API. Notebook and SQL query content is fetched from the shared workspace database via `IWorkspaceReader`, which reads from the same PostgreSQL database used by the UI server.

---

## Features

| Feature                        | Notes                                                                                                                                                                                                                                                                                                            |
| ------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **DAG execution**              | Tasks run in parallel as soon as their dependencies are satisfied                                                                                                                                                                                                                                                |
| **Dependency conditions**      | `OnSuccess`, `OnFailure`, `OnCompletion`, `OnSkip` per edge                                                                                                                                                                                                                                                      |
| **Skip propagation**           | Tasks whose dependency condition can never be satisfied are auto-skipped                                                                                                                                                                                                                                         |
| **Retries**                    | Per-task `MaxRetries` + `RetryInterval`; status cycles `Running → Retrying → Pending`                                                                                                                                                                                                                            |
| **Task timeout**               | Optional per-task `Timeout` field (passed to kernel execution)                                                                                                                                                                                                                                                   |
| **Three task types**           | `NotebookTask`, `SqlQueryTask`, `SubJobTask`                                                                                                                                                                                                                                                                     |
| **Sub-jobs**                   | A task can trigger another job and wait for its completion                                                                                                                                                                                                                                                       |
| **Scheduled execution**        | Quartz-backed cron triggers (6-field Quartz format) with per-schedule timezone and automatic DST handling; schedule changes take effect immediately without restart                                                                                                                                               |
| **Manual trigger**             | `POST /api/jobs/{id}/trigger` with optional parameter overrides                                                                                                                                                                                                                                                  |
| **Parameterised jobs**         | Named parameters with defaults; resolved into `${{ param_name }}` placeholders in task configs                                                                                                                                                                                                                   |
| **Notebook parameterisation**  | Injects a parameter cell after a cell tagged `parameters` before execution                                                                                                                                                                                                                                       |
| **`%run` magic**               | Python cells may use `%run path/to/notebook` to inline another notebook; recursive with cycle detection (depth limit 10)                                                                                                                                                                                         |
| **SQL wrapping**               | SQL cells and `SqlQueryTask` content are wrapped as `import duckdb; duckdb.sql("""...""")` before kernel execution                                                                                                                                                                                               |
| **Cell-level output tracking** | Each notebook cell has its own `TaskRunCellOutput` row with status, outputs, timing, and errors                                                                                                                                                                                                                  |
| **Node pool configs**          | Two types: `JobNodePoolConfig` (run-scoped nodes, deleted on run end) and `InteractiveNodePoolConfig` (stable pool node, persists until evicted). Stored in DB and referenced by tasks via `NodePoolRef`.                                                                                                        |
| **Warm node pools**            | `JobNodePoolConfig` supports a `WarmNodes` standby count and an optional `MaxNodes` cap. Warm standby nodes are pre-provisioned and instantly claimed when a job starts, eliminating cold-start wait time. `MaxNodes` bounds the total live node count (warm + active) across all concurrent runs for that pool. |
| **Node provisioning**          | `NodeManager` lazily provisions a node per distinct `NodePoolRef` per run, or claims a pre-warmed standby from `WarmPoolManager` when one is available. Job pool nodes are created fresh each run and deleted on completion; interactive pool nodes are joined if already running                                |
| **Per-task kernels**           | Each notebook/SQL task gets its own kernel, deleted immediately after the task completes                                                                                                                                                                                                                         |
| **Crash recovery**             | `RecoveryService` re-dispatches any `Running` or `Pending` runs found in the DB on startup                                                                                                                                                                                                                       |
| **Concurrent run cap**         | Per-job `MaxConcurrentRuns` limit enforced at trigger time                                                                                                                                                                                                                                                       |
| **Run cancellation**           | `POST /api/runs/{id}/cancel`; propagates a `CancellationToken` through the entire DAG execution                                                                                                                                                                                                                  |
| **Job snapshot**               | The full job definition (tasks, parameters) is snapshotted as JSON at trigger time; in-flight runs are immune to live job edits                                                                                                                                                                                  |
| **Job import / export**        | YAML format supporting tasks, parameters, schedules, and inline node pool definitions                                                                                                                                                                                                                            |
| **History retention**          | `HistoryRetentionService` purges old run records on a configurable schedule                                                                                                                                                                                                                                      |

---

## Project Structure

| Project                                 | Layer          | Role                                                                    |
| --------------------------------------- | -------------- | ----------------------------------------------------------------------- |
| `DuckHouse.Orchestrator`                | Host           | ASP.NET Core entry point; minimal API endpoints; Aspire wiring          |
| `DuckHouse.Orchestrator.Application`    | Application    | Engine services, mediator handlers, YAML import/export, DAG validation  |
| `DuckHouse.Orchestrator.Core`           | Domain         | Entity classes, enums, repository interfaces, infrastructure interfaces |
| `DuckHouse.Orchestrator.Infrastructure` | Infrastructure | `ControlPlaneClient` HTTP client; EF Core repositories                  |

---

## Core Domain Types

### `Job`

The static definition of a job. Owns a list of `JobParameter`, `JobTask`, and `JobSchedule` records. `MaxConcurrentRuns` caps the number of simultaneous active runs. `IsEnabled` gates scheduler-triggered runs.

### `JobParameter`

Declares a named parameter with an optional default value and a required flag. At trigger time, the caller may supply override values; unresolved required parameters cause validation errors.

### `JobTask` (abstract)

Base class for the task DAG nodes. Shared properties: `Name`, `MaxRetries`, `RetryInterval`, `Timeout`, and `Dependencies`. Stored with TPH discrimination and serialised polymorphically via `[JsonDerivedType]`.

| Concrete type  | Executes                                                                                                              |
| -------------- | --------------------------------------------------------------------------------------------------------------------- |
| `NotebookTask` | A workspace notebook identified by `NotebookId`. Requires `NodePoolRef`.                                              |
| `SqlQueryTask` | A workspace SQL query file identified by `QueryId`. Requires `NodePoolRef`.                                           |
| `SubJobTask`   | Another job identified by `SubJobId`. No node or kernel involved; waits for the child run to reach a terminal status. |

### `TaskDependency`

An edge in the task DAG. `DependsOnTaskId` points to the upstream task; `Condition` (`DependencyCondition` enum) specifies which terminal state of the upstream task satisfies this edge.

### `JobSchedule`

A cron rule attached to a job. `CronExpression` must be a valid Quartz 6-field expression (`seconds minutes hours day-of-month month day-of-week`), e.g. `0 30 8 * * ?` — 5-field Unix cron is rejected by the scheduler. `TimeZone` accepts any IANA or Windows timezone ID recognised by `TimeZoneInfo.FindSystemTimeZoneById`; falls back to server local time if blank. `NextFireTime` is a `[NotMapped]` computed `DateTimeOffset?` property calculated on demand from `CronExpression` and `TimeZone` using `Quartz.CronExpression` — it is never persisted and must never be assigned or read through EF queries. Optional `Parameters` override job-level parameter defaults for scheduled runs; required parameters with no default must be present here or the run will be rejected at trigger time.

### `NodePoolConfig` (abstract)

A named configuration for a compute node. Referenced from tasks via `NodePoolRef` string. Carries `VmSize`, `KernelRequirements` (pip requirements text), optional `WheelPackageIds`, `EnvironmentVariableIds`, and `SecretIds` (resolved at node provisioning time), and idle timeout settings forwarded to the Control Plane.

`NodePoolConfig` is **abstract** with TPH discrimination (column `PoolType varchar(32)`). The two concrete subtypes are:

| Subtype                     | PoolType        | Behaviour                                                                                                                                                                                                                                                                                                                                                                            |
| --------------------------- | --------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `JobNodePoolConfig`         | `"Job"`         | Run-scoped node; created on demand per run (or claimed from the warm standby pool), deleted when the run finishes. Optional fields: `WarmNodes` (number of pre-warmed standbys to maintain; default `0`), `MaxNodes` (total live node cap across warm + active; `null` = unlimited), `NodeAcquireTimeout` (how long to wait when `MaxNodes` is reached; `null` = wait indefinitely). |
| `InteractiveNodePoolConfig` | `"Interactive"` | Pool has a stable node name (`i` + 11 GUID hex chars); node is created on demand and persists until deleted by the inactivity eviction service                                                                                                                                                                                                                                       |

### `JobRun`

A single execution instance of a job. Tracks `Status` (`JobRunStatus` enum), `Trigger` (Manual / Scheduled / SubJob), optional `ParentRunId` / `ParentTaskRunId` for sub-job linkage, runtime `Parameters`, and a `SnapshotJson` of the job definition captured at trigger time. Owns a list of `TaskRun` records, one per task.

### `TaskRun`

Runtime state for one task within a job run. Tracks `Status` (`TaskRunStatus` enum), `AttemptNumber`, the `NodeName` and `KernelId` assigned for execution, timing, and error message. Notebook tasks additionally populate a `CellOutputs` list.

### `TaskRunCellOutput`

Granular execution record for one cell within a notebook task run. Stores the cell source, type, language, execution status (`CellExecutionStatus`), kernel outputs (as JSON), error details, execution count, and timing. The `CellRole` field marks `parameters` and `injected-parameters` cells.

---

## Orchestration Flow

### 1. Trigger

A run is created by `TriggerJobHandler` (command: `TriggerJobRequest`), which:

1. Loads the live `Job` from the database and checks `MaxConcurrentRuns`.
2. Serialises the full job definition into `SnapshotJson` — subsequent edits to the job have no effect on this run.
3. Creates a `JobRun` with status `Pending` and one `TaskRun` (status `Pending`, attempt 1) per task.
4. Persists everything to the database.
5. Calls `RunDispatcher.DispatchRun(runId)` to start background execution immediately.

Triggers come from three sources:

- **Manual**: `POST /api/jobs/{id}/trigger` (UI or external caller)
- **Scheduled**: `SchedulesManager` fires a Quartz cron trigger and calls `TriggerJobRequest` via `ScheduledJobExecutor`
- **Sub-job**: `TaskExecutor` calls `TriggerJobRequest` when executing a `SubJobTask`

### 2. Dispatch (`RunDispatcher`)

`RunDispatcher` is a **singleton** that tracks all in-flight runs. When `DispatchRun` is called:

1. A `CancellationTokenSource` is registered for the run ID in a `ConcurrentDictionary`.
2. A `Task.Run` is launched, which creates a DI scope and resolves a `RunCoordinator`.
3. After the run completes (or throws), the CTS is removed and disposed.

`CancelRunAsync(runId)` signals the run's CTS, which propagates cancellation through the entire DAG execution chain.

### 3. DAG Execution (`RunCoordinator`)

`RunCoordinator` is **scoped** (one instance per dispatched run). Its `ExecuteRunAsync` method:

1. Loads the `JobRun` from the database; deserialises the job from `SnapshotJson` (falls back to live DB for legacy runs without a snapshot).
2. Transitions the run to `Running` and records `StartedAt`.
3. Creates a **`NodeManager`** and a **`TaskExecutor`** for this run.
4. Enters the DAG loop (`RunDagAsync`).
5. On exit (normal, cancelled, or faulted), calls `NodeManager.CleanupAllAsync` and writes the final `JobRunStatus`.

**The DAG loop** runs until no tasks remain:

```
while (true):
  1. PropagateSkips   — mark tasks that can never run
  2. FindReadyTasks   — collect Pending tasks whose dependencies are all satisfied
  3. Dispatch each ready task as Task.Run → TaskExecutor.ExecuteAsync
  4. Await Task.WhenAny of all active tasks
  5. Remove completed tasks; persist updated TaskRun rows
  6. If status is Retrying: wait RetryInterval, reset to Pending, increment AttemptNumber
```

**Dependency resolution** in `FindReadyTasks`: a task is ready when every `TaskDependency` edge is satisfied by its upstream `TaskRun.Status` according to `DependencyCondition`:

| Condition      | Satisfied when upstream is                       |
| -------------- | ------------------------------------------------ |
| `OnSuccess`    | `Succeeded`                                      |
| `OnFailure`    | `Failed`                                         |
| `OnSkip`       | `Skipped`                                        |
| `OnCompletion` | `Succeeded`, `Failed`, `Skipped`, or `Cancelled` |

**Skip propagation** in `PropagateSkips`: iterates tasks repeatedly (fixed-point) and marks any `Pending` task as `Skipped` if at least one of its dependency edges can _never_ be satisfied given the upstream's terminal status. For example, a task that requires `OnSuccess` from an upstream that `Failed` will be skipped.

**Outcome determination**: after the loop, `DetermineOutcome` sets the final `JobRunStatus`:

- All tasks `Succeeded` or `Skipped` → `Succeeded`
- Any task `Cancelled` → `Cancelled`
- Otherwise → `Failed`

### 4. Node & Kernel Management (`NodeManager`, `WarmPoolManager`)

`NodeManager` is created per run by `RunCoordinator`. It manages the mapping from `NodePoolRef` names to provisioned nodes.

**`EnsureNodeAsync(nodePoolRef)`** — called before any kernel is created for a pool ref:

1. Returns the cached node name if the pool ref has already been provisioned for this run.
2. Loads the `NodePoolConfig` by name; branches on the concrete type:
   - **`JobNodePoolConfig`** with `WarmNodes > 0`: attempts to claim a pre-warmed standby via `WarmPoolManager.ClaimNodeAsync`. If a warm node is available it is returned immediately and re-provisioned as a run-scoped node (no cold-start wait). If no standby is available (e.g., on first run after startup), falls back to creating a fresh node. After claiming or creating, `WarmPoolManager.ScheduleReplenishment` queues replacement of the consumed standby.
   - **`JobNodePoolConfig`** with `WarmNodes == 0`: derives node name as `j` + 11 lowercase hex chars from SHA-256 of `(runId, poolRef)`. Creates the node via the control plane and polls until `Running`. If `MaxNodes` is set, the semaphore in `WarmPoolManager` is acquired first (blocking until a slot is free or `NodeAcquireTimeout` elapses).
   - **`InteractiveNodePoolConfig`**: derives node name via `pool.GetNodeName()` (`i` + 11 GUID hex chars). If the node is already `Running`, returns it immediately. Otherwise creates it (handling 409 race) and polls until `Running`. Sets `Provisioned = false` (not deleted on cleanup — eviction handles teardown).

**`CreateKernelAsync(nodePoolRef)`** — called by `TaskExecutor` per task:

1. Calls `EnsureNodeAsync` to get (or provision) the node.
2. Calls `IControlPlaneClient.CreateKernelAsync` and returns `(nodeName, kernelId)`.

**`CleanupKernelAsync`** — called by `TaskExecutor` in the `finally` block of each task, regardless of success or failure.

**`CleanupAllAsync`** — called by `RunCoordinator` in its `finally` block:

- Job pool nodes with `Provisioned = true` are deleted via the control plane; their semaphore slot in `WarmPoolManager` is released.
- Interactive pool nodes (`Provisioned = false`) are left running.

---

### `WarmPoolManager` (singleton)

`WarmPoolManager` is a **singleton** responsible for the lifecycle of warm standby nodes across all concurrent runs. It maintains:

- A `ConcurrentQueue<string>` of available warm node names per pool ID.
- A `SemaphoreSlim(MaxNodes, MaxNodes)` per pool (when `MaxNodes` is configured) that limits the total live node count (warm + active jobs combined).

**Key operations:**

| Method                          | Description                                                                                                                                                                                                                                                                                                                                   |
| ------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `InitialiseAsync(configs)`      | Called on startup by `WarmPoolReplenishmentService`. Queries the control plane for already-running nodes whose names start with the pool's warm-node prefix (`w` + 7 GUID hex chars) and enqueues them as pre-existing standbys.                                                                                                              |
| `ClaimNodeAsync(pool, ct)`      | Dequeues one warm standby node name if available. Returns `null` when the queue is empty (caller falls back to cold provisioning). Does **not** acquire a semaphore slot (the slot was already held when the warm node was created).                                                                                                          |
| `ReleaseSlotAsync(poolId)`      | Releases one `MaxNodes` semaphore slot when a node (warm or job-scoped) is deleted.                                                                                                                                                                                                                                                           |
| `AdjustPoolAsync(pool)`         | Called when a pool's `WarmNodes` or `MaxNodes` is changed in the UI. Evicts excess warm standbys down to the new `WarmNodes` count; rebuilds the `MaxNodes` semaphore to reflect the updated cap.                                                                                                                                             |
| `ReplenishAsync(pool)`          | Ensures the warm standby queue is filled up to `WarmNodes`. Acquires a semaphore slot (if `MaxNodes` applies), creates a node named `w` + 7-char pool GUID prefix + 4 random hex chars, and enqueues it. Warm nodes are created with `NodeIdleTimeout = TimeSpan.Zero` so the control plane's inactivity eviction service never deletes them. |
| `ScheduleReplenishment(poolId)` | Queues an async replenishment task after a warm node is claimed, so a fresh standby is available for the next job.                                                                                                                                                                                                                            |

**Warm node naming**: `w` + first 7 hex chars of the pool GUID + 4 random hex = 12 characters total. The 8-character prefix (`w` + 7 chars) uniquely identifies which pool a warm node belongs to, allowing `InitialiseAsync` to rediscover existing standbys after a restart.

**Eviction protection**: warm standbys are created with `NodeIdleTimeout = TimeSpan.Zero`. The control plane's `InactivityEvictionService` treats `Zero` as "never evict". When a warm node is claimed by a job, `NodeManager` calls `UpdateNodeEvictionConfigAsync` to restore the pool's normal idle timeout so the node can be evicted at the end of the job (or when the run's `CleanupAllAsync` deletes it explicitly).

### 5. Task Execution (`TaskExecutor`)

`TaskExecutor` is **scoped**. Its `ExecuteAsync` method dispatches to one of three strategies based on the concrete task type.

#### NotebookTask

1. Fetches notebook JSON from `IWorkspaceReader`.
2. Parses cells (`ParseNotebookCells`): produces a `CellInfo` list with source, type (`Code` / `Markdown`), language (`Python` / `Sql`), and tags.
3. If the notebook has a `parameters`-tagged cell and the task has parameters, injects an `injected-parameters` cell immediately after it, containing resolved parameter assignments.
4. Creates a `TaskRunCellOutput` record for each cell (`Pending`).
5. Calls `NodeManager.CreateKernelAsync` to get a kernel; records `NodeName` and `KernelId` on the `TaskRun`.
6. Iterates code cells sequentially:
   - SQL cells are wrapped as `import duckdb; duckdb.sql("""...""")`.
   - Python cells are expanded for `%run` magic lines (recursively, up to depth 10).
   - Calls `ExecuteCodeAsync` (start execution → poll every second until complete).
   - Updates the `TaskRunCellOutput` with status, outputs, error, and timing.
   - On any cell error, marks remaining code cells as `Skipped` and throws.
7. Deletes the kernel in `finally`.

#### SqlQueryTask

1. Fetches query content from `IWorkspaceReader`.
2. Wraps the SQL as `import duckdb; duckdb.sql("""...""")`.
3. Calls `NodeManager.CreateKernelAsync`, executes the wrapped SQL, stores `QueryResultJson` on the `TaskRun`.
4. Deletes the kernel in `finally`.

#### SubJobTask

1. Sends `TriggerJobRequest` with `JobRunTrigger.SubJob` and resolved parameters.
2. Sets `ParentRunId` / `ParentTaskRunId` on the child `JobRun`.
3. Polls `GetJobRunAsync` every 5 seconds until the child run reaches a terminal status.
4. Returns normally on `Succeeded`, throws `InvalidOperationException` on `Failed`, throws `OperationCanceledException` on `Cancelled`.

### 6. Background Services

#### `WarmPoolReplenishmentService`

Runs as a `BackgroundService`. On startup it calls `WarmPoolManager.InitialiseAsync` to rediscover any warm standby nodes already running from a previous process instance. Then, every 60 seconds, it loads all `JobNodePoolConfig` records with `WarmNodes > 0` and calls `WarmPoolManager.ReplenishAsync` for each — ensuring the warm standby queues stay filled even if nodes are unexpectedly lost.

#### `SchedulesManager`

Singleton `BackgroundService` that wraps a Quartz in-memory scheduler. On startup it loads all `JobSchedule` rows from the database and registers a Quartz cron trigger for each one; disabled schedules are registered but paused.

`ScheduledJobExecutor` (Quartz `IJob`) fires when a trigger is due:
1. Loads the job and verifies it is enabled.
2. Finds the matching `JobSchedule` on `job.Schedules` to obtain parameter overrides.
3. Calls `TriggerJobRequest` with those overrides — `TriggerJobHandler` then merges them with job-schema defaults and validates required parameters.

Quartz stores all trigger fire times as UTC and recomputes the next fire time through the configured `TimeZoneInfo` after each execution, so DST transitions are handled automatically without a restart. The misfire policy is `DoNothing`: if the service was down when a trigger was due, that instance is skipped.

**Immediate updates**: `CreateScheduleHandler`, `UpdateScheduleHandler`, `DeleteScheduleHandler`, and `DeleteJobHandler` call the corresponding `SchedulesManager` method immediately after persisting to the database — no polling or restart needed.

#### `RecoveryService`

Runs **once** on startup (after a 3-second warm-up delay). Queries the database for all runs in `Running` or `Pending` status and re-dispatches them through `RunDispatcher`. This makes the orchestrator resilient to process restarts — in-progress runs resume from their current persisted task state.

#### `HistoryRetentionService`

Runs every `History:PurgeIntervalHours` hours (default 1). Deletes `JobRun` records (and their cascaded `TaskRun` and `TaskRunCellOutput` children) that completed before `now - History:RetentionDays` days (default 30). Set `History:RetentionDays` to `0` or less to disable automatic purging. A manual purge endpoint is also available at `POST /api/admin/purge-history`.

---

## Status Lifecycles

### `JobRunStatus`

```
Pending ──► Running ──► Succeeded
                   └──► Failed
                   └──► Cancelled
```

### `TaskRunStatus`

```
Pending ──► Running ──► Succeeded
                   └──► Failed ──► (if retries remain) Retrying ──► Pending
                   └──► Cancelled
                   └──► Skipped   (dependency can never be satisfied)
```

`Waiting` is defined in the enum but is not currently assigned by the engine; it is reserved for future use.

---

## API Endpoints

| Method   | Path                                     | Description                                                |
| -------- | ---------------------------------------- | ---------------------------------------------------------- |
| `GET`    | `/api/jobs`                              | List all jobs                                              |
| `GET`    | `/api/jobs/{id}`                         | Get a job                                                  |
| `POST`   | `/api/jobs`                              | Create a job                                               |
| `PUT`    | `/api/jobs/{id}`                         | Update a job                                               |
| `DELETE` | `/api/jobs/{id}`                         | Delete a job                                               |
| `POST`   | `/api/jobs/{id}/trigger`                 | Trigger a run (optional body: `{ parameters: {} }`)        |
| `GET`    | `/api/jobs/{id}/runs`                    | List runs for a job (`limit`, `offset`)                    |
| `GET`    | `/api/jobs/{id}/schedules`               | List schedules for a job                                   |
| `POST`   | `/api/jobs/{id}/schedules`               | Create a schedule                                          |
| `PUT`    | `/api/jobs/{id}/schedules/{sid}`         | Update a schedule                                          |
| `DELETE` | `/api/jobs/{id}/schedules/{sid}`         | Delete a schedule                                          |
| `POST`   | `/api/jobs/import`                       | Import a job from YAML (`{ yaml: "..." }`)                 |
| `GET`    | `/api/jobs/{id}/export`                  | Export a job as YAML                                       |
| `GET`    | `/api/runs/{id}`                         | Get a job run                                              |
| `POST`   | `/api/runs/{id}/cancel`                  | Cancel an active run                                       |
| `GET`    | `/api/runs/{id}/tasks/{taskRunId}`       | Get a task run                                             |
| `GET`    | `/api/runs/{id}/tasks/{taskRunId}/cells` | Get cell outputs for a notebook task run                   |
| `GET`    | `/api/node-pools`                        | List node pool configurations                              |
| `GET`    | `/api/node-pools/{id}`                   | Get a node pool configuration                              |
| `POST`   | `/api/node-pools`                        | Create a node pool configuration                           |
| `PUT`    | `/api/node-pools/{id}`                   | Update a node pool configuration                           |
| `DELETE` | `/api/node-pools/{id}`                   | Delete a node pool configuration                           |
| `POST`   | `/api/admin/purge-history`               | Manually purge run history (body: `{ retentionDays: 30 }`) |

---

## YAML Job Format

Jobs can be defined in YAML and imported via `POST /api/jobs/import`. The importer resolves notebook and query workspace paths to IDs, looks up sub-job references by name, and validates the DAG for cycles before persisting.

```yaml
name: My Pipeline
description: Optional description
maxConcurrentRuns: 1

parameters:
  - name: run_date
    defaultValue: "2024-01-01"
    required: false
    description: The date to process

nodePools:
  - name: standard
    vmSize: Standard_D4s_v3
    kernelRequirements: |
      pandas==2.2.0
      scikit-learn

tasks:
  - name: Prepare
    type: notebook
    notebookPath: /pipelines/prepare.ipynb
    nodePoolRef: standard
    maxRetries: 1
    retryInterval: "00:01:00"
    timeout: "01:00:00"
    parameters:
      run_date: ${{ run_date }}

  - name: Transform
    type: sqlquery
    queryPath: /pipelines/transform.sql
    nodePoolRef: standard
    dependencies:
      - task: Prepare
        condition: onSuccess

  - name: Notify
    type: subjob
    jobName: notification-job
    parameters:
      status: completed
    dependencies:
      - task: Transform
        condition: onCompletion

schedules:
  - cron: "0 0 2 * * ?"
    timeZone: "Europe/Helsinki"
    parameters:
      run_date: "auto"
```

Valid dependency conditions: `onSuccess`, `onFailure`, `onCompletion`, `onSkip`.

---

## Configuration

| Key                                    | Default                | Description                                                      |
| -------------------------------------- | ---------------------- | ---------------------------------------------------------------- |
| `ControlPlane:BaseAddress`             | `http://control-plane` | Base URL of the Control Plane service                            |
| `History:RetentionDays`                | `30`                   | Age (in days) at which completed runs are purged; `≤ 0` disables |
| `History:PurgeIntervalHours`           | `1`                    | How often `HistoryRetentionService` runs the purge               |

The shared PostgreSQL connection is registered under the Aspire resource name `duckhouse-ui` (same database used by the UI server).

---

## DuckLake Catalogs

When a notebook or SQL task runs, the orchestrator automatically attaches any DuckLake catalogs that are associated with the workspace item. Before the first cell is executed, it generates and sends a Python setup script to the kernel that:

1. Installs and loads the `ducklake` DuckDB extension (and the `azure` extension if Azure storage is used).
2. Creates a DuckDB `SECRET` for the Postgres catalog metadata store.
3. Creates a DuckDB `SECRET` for Azure blob storage (if applicable).
4. Executes `ATTACH 'ducklake:postgres:' AS catalog_name (DATA_PATH '...')`.

Catalog connection details for **managed** catalogs are read entirely from the `Catalogs` configuration section — nothing is stored in the database. **External** catalogs store their connection details (encrypted via ASP.NET Data Protection) in the database.

### Configuration

| Key                                | Default                         | Description                                                                                                                                              |
| ---------------------------------- | ------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Catalogs:BaseDataPath`            | _(none)_                        | Base path for managed catalog parquet data. Final path = `BaseDataPath/catalogName`. Can be a local path or `abfss://` for Azure Data Lake               |
| `Catalogs:StorageConnectionString` | _(none)_                        | Azure Data Lake connection string. Required when `BaseDataPath` uses `az://` or `abfss://`                                                               |
| `Catalogs:CatalogHost`             | `localhost`                     | Postgres host for **direct connections** made by the orchestrator process itself (admin operations)                                                      |
| `Catalogs:CatalogPodHost`          | _(falls back to `CatalogHost`)_ | Postgres host as seen **from inside kernel pods**. Set this when the pod-side address differs from the server-side address (see local development below) |
| `Catalogs:CatalogPort`             | `5432`                          | Postgres port                                                                                                                                            |
| `Catalogs:CatalogUser`             | _(none)_                        | Postgres user                                                                                                                                            |
| `Catalogs:CatalogPassword`         | _(none)_                        | Postgres password                                                                                                                                        |

### Local development

Kernel pods run in Kubernetes (Docker Desktop) and cannot reach `localhost` on the developer machine. Set `CatalogPodHost` to `host.docker.internal` so the DuckDB `CREATE SECRET` commands inside pods resolve to the host:

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

`CatalogHost` continues to be used for any direct Postgres connections made by the orchestrator process. `CatalogPodHost` is only injected into the DuckDB setup scripts executed by kernel pods. In production, where both the service and pods reach Postgres via the same Kubernetes service DNS name, `CatalogPodHost` can be left unset.

For persistent parquet storage across pod restarts, set `BaseDataPath` to the `DataVolumeMountPath` configured in the Control Plane's `NodeService:Local` section. See the [Control Plane README](../control-plane/README.md#persistent-data-volume-local-development) for details.
