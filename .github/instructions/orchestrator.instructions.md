---
applyTo: src/orchestrator/**
---

# DuckHouse Orchestrator

ASP.NET Core Web API that schedules and executes multi-task jobs against compute nodes managed by the Control Plane. Follows the same Clean Architecture and custom mediator pattern as the UI server and Control Plane.

Shared domain types (`NodeInfo`, `KernelInfo`, mediator interfaces) live in `src/shared/DuckHouse.Core`. The orchestrator shares the same PostgreSQL database as the UI server — Aspire resource name `duckhouse-ui`. There is no separate database.

## Engine service lifetime model

`RunDispatcher` is a **singleton** because it must track every in-flight run across DI scopes (one dictionary entry per active run). `RunCoordinator` and `TaskExecutor` are **scoped** — `RunDispatcher` creates a fresh DI scope for each run so they get their own EF Core `DbContext`. Never make `RunCoordinator` a singleton or inject a scoped service directly into `RunDispatcher`.

`WarmPoolManager` is a **singleton** (it owns warm queues and semaphores that must outlive individual runs). `IWheelPackageReader`, `IEnvironmentResolver`, and `IControlPlaneClient` are singletons so they can be injected into `WarmPoolManager`; they use `IServiceScopeFactory` to obtain a fresh `DbContext` per operation. `IJobRepository` must remain **scoped**: `UpdateJobAsync` relies on EF change tracking across calls for child entity orphan deletion and cannot be safely converted to a per-operation context.

## Job snapshot

When a run is triggered, `TriggerJobHandler` serialises the complete `Job` entity as `SnapshotJson` on the `JobRun`. `RunCoordinator` executes from this snapshot, not the live database — editing a job never affects a run already in progress or recovering from a crash.

## DAG execution

**Skip propagation is eager**: a task is skipped as soon as any single dependency edge becomes permanently unsatisfiable. Downstream tasks are re-evaluated in the next fixed-point iteration.

**Retries reset in-place**: on failure the same `TaskRun` row is reused — `AttemptNumber` is incremented and status reset to `Pending`. A new `TaskRun` row is never created for a retry.

**Outcome rule**: the run succeeds only if every task ended `Succeeded` or `Skipped`. Any `Cancelled` task → `Cancelled` run. Anything else → `Failed`.

## Node and kernel management

**One node per `NodePoolRef` per run.** Tasks that share the same `NodePoolRef` share the same node — by design, to avoid redundant provisioning costs.

**Interactive vs Job pool behaviour in `NodeManager`:**

- **`JobNodePoolConfig`** — see *Warm node pools* below for full routing logic.
- **`InteractiveNodePoolConfig`** — uses the stable pool node name from `GetNodeName()`. Marked `Provisioned = false` so `CleanupAllAsync` **does not delete it** — the inactivity eviction service handles teardown. When a job task references an interactive pool, the node is joined but never stopped when the job completes.

**One kernel per task.** Kernels are not shared between tasks — this ensures no Python state leaks.

**Node cleanup** (`CleanupAllAsync`) runs with `CancellationToken.None` so it executes even after cancellation. Only nodes with `Provisioned = true` are deleted.

Node name prefixes: `j` = job-scoped, `i` = interactive, `w` = warm standby. The 63-character Kubernetes name limit applies; keep `NodePoolRef` names short.

## Warm node pools

`JobNodePoolConfig` carries three fields controlling warm standby behaviour: `WarmNodes` (target standby count, default 0), `MaxNodes` (total live node cap including warm standbys; `null` = unlimited), and `NodeAcquireTimeout` (how long to wait when `MaxNodes` is reached; `null` = wait indefinitely).

**`WarmPoolManager`** (singleton) owns a `ConcurrentQueue<string>` of ready warm node names and a `SemaphoreSlim` for `MaxNodes` enforcement, both keyed by pool ID. Critical invariants:

- Warm standbys are created with `NodeIdleTimeout = TimeSpan.Zero`. The control plane's `InactivityEvictionService` treats Zero as "never evict". When a warm node is claimed by a job, `UpdateNodeEvictionConfigAsync` must be called to restore the pool's normal timeout before handing the node to the run.
- The `MaxNodes` semaphore counts every live node (warm + active). Claiming a warm standby does **not** acquire a new slot — the slot was already held when the warm node was created. Only cold-provisioned nodes and new warm standbys acquire slots.
- When `WarmNodes` or `MaxNodes` changes via the UI, `UpdateNodePoolConfigHandler` must call `WarmPoolManager.AdjustPoolAsync` to evict excess standbys and rebuild the semaphore atomically.

**`WarmPoolReplenishmentService`** (BackgroundService) calls `InitialiseAsync` on startup to rediscover warm nodes from a previous process instance (by node-name prefix scan), then `ReplenishAsync` periodically. It resolves `INodePoolConfigRepository` via `IServiceScopeFactory` because the repository is scoped.

**`NodeManager` warm routing**: if `WarmNodes > 0`, `EnsureWarmJobNodeAsync` tries `ClaimNodeAsync` first; if the queue is empty it falls back to cold provisioning. In both cases `ScheduleReplenishment` queues a replacement. If `WarmNodes == 0` but `MaxNodes` is set, the cold path still acquires the semaphore before creating a node.

## Node pool config types

`NodePoolConfig` is **abstract** with TPH (discriminator `PoolType varchar(32)`). `NodePoolConfig` and its subtypes carry `[JsonPolymorphic]` / `[JsonDerivedType]` attributes — without these, `System.Text.Json` silently drops subtype-only fields when serialising by the declared abstract type. Always keep these attributes when adding new subtypes.

`YamlJobImporter` always creates `JobNodePoolConfig` rows for inline `nodePools` entries (job pools are the only pool type appropriate for YAML pipelines).

When adding a new task type: add it to `JobTask`'s `[JsonDerivedType]` attributes, add a case to `TaskExecutor.ExecuteAsync`, and add corresponding branches in `TriggerJobHandler` and `YamlJobImporter`.

## Notebook execution details

SQL cells and `SqlQueryTask` content are wrapped as DuckDB Python calls — the kernel environment only runs Python. Parameter injection inserts an `injected-parameters` cell after the `parameters`-tagged cell before execution; the injected cell is never stored back to the notebook source.

## Recovery

`RecoveryService` re-dispatches any `JobRun` with status `Running` or `Pending` on startup. The recovery path is the same code path as normal execution — `TaskRun` rows already in a terminal state are simply skipped by the DAG loop. No special recovery logic exists beyond the re-dispatch.

## Scheduling

Scheduling uses Quartz.NET (`SchedulesManager`, a singleton `BackgroundService`). On startup all `JobSchedule` rows are loaded and registered as Quartz cron triggers. When a trigger fires, `ScheduledJobExecutor` verifies the job is enabled, reads `schedule.Parameters` overrides, and calls `TriggerJobRequest`.

**`JobSchedule.NextFireTime`** is a `[NotMapped]` computed `DateTimeOffset?` property — derived from `CronExpression` + `TimeZone` via `Quartz.CronExpression.GetNextValidTimeAfter()` + `TimeZoneInfo.ConvertTime`. Never persisted; never needs to be manually assigned. Do not add it back to migrations.

**Cron format**: Quartz 6-field only (`seconds minutes hours day-of-month month day-of-week`). 5-field Unix cron is rejected. Example: `0 30 8 * * ?`.

**Immediate updates**: `CreateScheduleHandler`, `UpdateScheduleHandler`, `DeleteScheduleHandler`, and `DeleteJobHandler` each call `SchedulesManager` directly after DB save — no polling lag.

**Parameter validation**: `TriggerJobHandler.BuildEffectiveParameters` merges caller overrides with `DefaultValue` from the job schema. Any `IsRequired` parameter still missing after the merge causes an `InvalidOperationException` — the run is never created.

## YAML import

`YamlJobImporter` resolves workspace paths to IDs at import time. IDs are stored; paths are not. If an item is later moved or renamed, the stored ID still resolves correctly.

## Adding new features

- **New mediator command/query**: add to `Application/Mediator/Commands/` or `Queries/`. The mediator scans by assembly.
- **New endpoint**: add a static class in `DuckHouse.Orchestrator/Endpoints/` and register it in `Program.cs`.
- **New entity / EF migration**: uses `DuckHouseDbContext` from `src/shared`. Migrations live in `DuckHouse.Ui.Server` — run `dotnet ef migrations add` from that project.
