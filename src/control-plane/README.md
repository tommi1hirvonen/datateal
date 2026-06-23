# Datateal Control Plane

ASP.NET Core Web API that provisions and manages compute nodes and Jupyter kernels on demand. The Orchestrator and UI use it as the single gateway to all kernel execution. All kernel calls are tunnelled through the Kubernetes API server's HTTP proxy — no public IPs, Services, or VNet access are required.

---

## How the Control Plane Fits in the Solution

```
┌──────────────────────────────────────────────────────────────────────┐
│  UI Server / Orchestrator                                            │
│  • Creates nodes (via POST /nodes)                                   │
│  • Creates kernels, sends code for execution, polls results          │
└──────────────────────┬───────────────────────────────────────────────┘
                       │ HTTP (REST)
                       ▼
┌──────────────────────────────────────────────────────────────────────┐
│  Control Plane (this service)                                        │
│  • Provisions / removes compute nodes                                │
│  • Creates / deletes Jupyter kernels on those nodes                  │
│  • Tunnels kernel API calls through the K8s API server proxy         │
│  • Evicts idle kernels and deletes idle nodes automatically          │
└──────────────────────┬───────────────────────────────────────────────┘
                       │ Kubernetes API + HTTP proxy
                       ▼
┌──────────────────────────────────────────────────────────────────────┐
│  Runtime pods (datateal-runtime)                                    │
│  • FastAPI service managing Jupyter kernels                          │
│  • Kernel venv: Python + DuckDB + optional extras                    │
└──────────────────────────────────────────────────────────────────────┘
```

---

## Project Structure

| Project                                | Layer          | Role                                                            |
| -------------------------------------- | -------------- | --------------------------------------------------------------- |
| `Datateal.ControlPlane`                | Host           | ASP.NET Core entry point; minimal API endpoints; Aspire wiring  |
| `Datateal.ControlPlane.Application`    | Application    | Mediator handlers; `InactivityEvictionService`                  |
| `Datateal.ControlPlane.Core`           | Domain         | `INodeService`, `INodeRuntimeClient` interfaces                 |
| `Datateal.ControlPlane.Infrastructure` | Infrastructure | `LocalNodeService`, `AksNodeService`, `KubernetesRuntimeClient` |

Shared domain types (`NodeInfo`, `NodeState`, `KernelInfo`, kernel request/response models, mediator interfaces) live in `src/shared/Datateal.Core`.

---

## Node Backends

The active backend is selected by `NodeService:Backend` in configuration.

### `Local` — Docker Desktop

`LocalNodeService` creates Kubernetes pods in the `default` namespace of the Docker Desktop cluster. All pods use the locally-built `datateal-runtime:latest` image with `ImagePullPolicy: Never`.

- Node names are the pod names.
- A `ManagedByLabel` (`app.kubernetes.io/managed-by=datateal-control-plane`) marks all managed pods so they can be listed and cleaned up.

**Wheel packages** are delivered as Kubernetes ConfigMaps mounted read-only into the pod. Each ConfigMap is given the pod as an owner reference so it is garbage-collected when the pod is deleted.

**Secrets** (sensitive environment variables) are stored in a Kubernetes `Secret` object (named `env-{podName}`) and mounted as individual environment variables via `secretKeyRef`. The Secret is also owner-referenced to the pod.

#### Persistent data volume (local development)

By default pods have ephemeral storage only. To persist DuckLake parquet files across pod restarts, configure a host directory mount:

```json
// control-plane appsettings.Development.json
"NodeService": {
  "Local": {
    "DataVolumeHostPath": "/run/desktop/mnt/host/c/Users/YOU/data/ducklake",
    "DataVolumeMountPath": "/data/ducklake"
  }
}
```

When both settings are provided, `LocalNodeService` adds a `hostPath` volume (type `DirectoryOrCreate`) and a matching container volume mount to every pod it creates. The path `/run/desktop/mnt/host/c/` is the Docker Desktop WSL2 VM's way of addressing the Windows `C:\` drive — note the `host` segment:

| Context                                   | Path format                                         |
| ----------------------------------------- | --------------------------------------------------- |
| Windows path                              | `C:\Users\YOU\data\ducklake`                        |
| Ubuntu WSL2 distro                        | `/mnt/c/Users/YOU/data/ducklake`                    |
| Docker Desktop WSL2 VM (`docker-desktop`) | `/run/desktop/mnt/host/c/Users/YOU/data/ducklake` ✓ |

The directory must exist on the host before pods are created (Kubernetes `hostPath` does not create it):

```powershell
mkdir C:\Users\YOU\data\ducklake
```

Then set `Catalogs:BaseDataPath` in the UI server and orchestrator to the same value as `DataVolumeMountPath`:

```json
// UI server / orchestrator appsettings.Development.json
"Catalogs": {
  "BaseDataPath": "/data/ducklake"
}
```

### `Aks` — Azure Kubernetes Service

`AksNodeService` creates one AKS user-mode agent pool (one VM, one node) per requested node and deploys a single runtime pod pinned to that pool via `nodeSelector`. Removing a node deletes the pod first, then the agent pool.

**Authentication**: the cluster has `disableLocalAccounts: true`. The service fetches a kubeconfig from AKS ARM and then replaces the `kubelogin` exec plugin with `AksTokenProvider`, which acquires Entra ID tokens using the registered `TokenCredential`. When `TenantId`, `ClientId`, and `ClientSecret` are all set, `ClientSecretCredential` is used; otherwise it falls back to `DefaultAzureCredential`.

- Node names must be ≤ 63 characters (Kubernetes limit). Names assigned by the orchestrator use `j` + 11 hex chars (job pools) or `i` + 11 GUID hex chars (interactive pools).

---

## Kernel Proxy

`KubernetesRuntimeClient` forwards all kernel API calls to the runtime pod through the Kubernetes API server HTTP proxy path:

```
/api/v1/namespaces/default/pods/{pod}:8000/proxy/{path}
```

This means:

- No Kubernetes Service object is needed for the runtime
- No public IP or VNet access is required
- The Control Plane only needs network access to the K8s API server

---

## Inactivity Eviction

`InactivityEvictionService` is a `BackgroundService` that runs on a configurable `CheckInterval`:

1. Deletes kernels whose `LastActivity` exceeds `KernelIdleTimeout`.
2. **Deletes** nodes where all kernels have been idle for longer than `NodeIdleTimeout` (measured from the most recent kernel activity on that node). This applies to both interactive pool nodes left idle by users and job pool nodes leaked by orchestrator failures.

Eviction can be disabled entirely by setting `InactivityEviction:Enabled` to `false`.

---

## API

All routes are under `/nodes`. Kernel routes are nested under `/nodes/{name}/kernels`.

| Method   | Path                                                        | Description                                                                                      |
| -------- | ----------------------------------------------------------- | ------------------------------------------------------------------------------------------------ |
| `GET`    | `/nodes`                                                    | List all managed nodes                                                                           |
| `GET`    | `/nodes/{name}`                                             | Get a node                                                                                       |
| `POST`   | `/nodes`                                                    | Create a node (provisions a pod or AKS agent pool)                                               |
| `DELETE` | `/nodes/{name}`                                             | Remove a node                                                                                    |
| `PUT`    | `/nodes/{name}/config`                                      | Update per-node eviction timeouts (used by UI server when an interactive pool's config is saved) |
| `GET`    | `/nodes/{name}/kernels`                                     | List kernels on a node                                                                           |
| `POST`   | `/nodes/{name}/kernels`                                     | Create a kernel                                                                                  |
| `GET`    | `/nodes/{name}/kernels/{kernelId}`                          | Get a kernel                                                                                     |
| `DELETE` | `/nodes/{name}/kernels/{kernelId}`                          | Delete a kernel                                                                                  |
| `POST`   | `/nodes/{name}/kernels/{kernelId}/execute`                  | Start code execution → HTTP 202 + `executionId`                                                  |
| `GET`    | `/nodes/{name}/kernels/{kernelId}/executions/{executionId}` | Poll execution until `isComplete`                                                                |
| `POST`   | `/nodes/{name}/kernels/{kernelId}/restart`                  | Restart a kernel                                                                                 |
| `POST`   | `/nodes/{name}/kernels/{kernelId}/interrupt`                | Interrupt a running execution                                                                    |
| `POST`   | `/nodes/{name}/kernels/{kernelId}/completions`              | Code completions (Jedi-backed)                                                                   |
| `POST`   | `/nodes/{name}/kernels/{kernelId}/diagnostics`              | Syntax and lint diagnostics (pyflakes-backed)                                                    |

Execution is **async/poll**: `POST .../execute` returns HTTP 202 with an `{ executionId }` body; poll the executions endpoint until `isComplete: true`.

---

## Configuration

### `NodeService`

| Key                   | Default | Description                    |
| --------------------- | ------- | ------------------------------ |
| `NodeService:Backend` | `Local` | Node backend: `Local` or `Aks` |

### `NodeService:Local`

| Key                                     | Default          | Description                                                                                                                                               |
| --------------------------------------- | ---------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `NodeService:Local:KubeContext`         | `docker-desktop` | kubeconfig context name. `null` = use current-context                                                                                                     |
| `NodeService:Local:DataVolumeHostPath`  | _(none)_         | Host path (Docker Desktop WSL2 format) to mount into pods for persistent storage. See [Persistent data volume](#persistent-data-volume-local-development) |
| `NodeService:Local:DataVolumeMountPath` | _(none)_         | Mount path inside the pod. Must match `Catalogs:BaseDataPath` in the UI server / orchestrator                                                             |

### `NodeService:Aks`

| Key                                 | Default            | Description                                                                |
| ----------------------------------- | ------------------ | -------------------------------------------------------------------------- |
| `NodeService:Aks:SubscriptionId`    | —                  | Azure subscription ID                                                      |
| `NodeService:Aks:ResourceGroupName` | —                  | Resource group containing the AKS cluster                                  |
| `NodeService:Aks:ClusterName`       | —                  | AKS cluster name                                                           |
| `NodeService:Aks:DefaultVmSize`     | `Standard_D4as_v5` | VM size for new agent pools                                                |
| `NodeService:Aks:RuntimeImage`      | —                  | Full image reference, e.g. `myregistry.azurecr.io/datateal-runtime:latest` |
| `NodeService:Aks:NodeSubnetId`      | —                  | Subnet resource ID for new agent pools                                     |
| `NodeService:Aks:TenantId`          | _(none)_           | Entra ID tenant (service principal auth)                                   |
| `NodeService:Aks:ClientId`          | _(none)_           | App registration client ID (service principal auth)                        |
| `NodeService:Aks:ClientSecret`      | _(none)_           | Client secret — use secrets manager, not appsettings                       |

### `InactivityEviction`

| Key                                    | Default    | Description                                           |
| -------------------------------------- | ---------- | ----------------------------------------------------- |
| `InactivityEviction:Enabled`           | `true`     | Set to `false` to disable eviction entirely           |
| `InactivityEviction:KernelIdleTimeout` | `00:10:00` | Delete kernels idle longer than this                  |
| `InactivityEviction:NodeIdleTimeout`   | `00:20:00` | Delete nodes with no kernel activity longer than this |
| `InactivityEviction:CheckInterval`     | `00:01:00` | How often the eviction sweep runs                     |

---

## Security Considerations

### Kernel Environment Access

Kernel processes inherit the pod's environment variables. User code executing in a kernel session can access all environment variables via `os.environ`, including any secrets injected as Kubernetes Secret references. Administrators should:

- Inject only the minimum required environment variables into runtime pods
- Use scoped credentials with minimal privileges
- Monitor kernel execution logs for suspicious activity
