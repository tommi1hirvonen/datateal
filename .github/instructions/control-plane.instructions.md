---
applyTo: src/control-plane/**
---

# DuckHouse Control Plane

ASP.NET Core 10 Web API (.NET Aspire, `DuckHouse.ControlPlane.slnx`) that dynamically provisions compute nodes and manages Jupyter kernels running on them.

## Projects (Clean Architecture)

| Project | Role |
|---|---|
| `DuckHouse.ControlPlane` | ASP.NET Core host; minimal API endpoints, Aspire wiring |
| `DuckHouse.ControlPlane.Application` | Use-case layer: mediator handlers, `InactivityEvictionService` |
| `DuckHouse.ControlPlane.Core` | Domain interfaces: `INodeService`, `INodeRuntimeClient` |
| `DuckHouse.ControlPlane.Infrastructure` | Implementations: `LocalNodeService`, `AksNodeService`, `KubernetesRuntimeClient` |

Domain types (`NodeInfo`, `NodeState`, `KernelInfo`, `KernelStatus`, kernel request/response models, mediator interfaces) live in `src/shared/DuckHouse.Core`, which is also referenced by the UI server.

## Node backends

`INodeService` has two implementations selected by `NodeService:Backend` in config:

| Backend | Class | "Node" concept |
|---|---|---|
| `Local` | `LocalNodeService` | Kubernetes pod in Docker Desktop |
| `Aks` | `AksNodeService` | AKS agent pool (one VM, one pod) |

**Local**: creates pods from `duckhouse-runtime:latest` with `ImagePullPolicy: Never`. Uses `~/.kube/config` with context from `NodeService:Local:KubeContext` (default `"docker-desktop"`). Stop/Start are no-ops.

**AKS**: creates an AKS agent pool (`Count=1`, `Mode=User`) then deploys one pod pinned to that pool via `nodeSelector`. Pods are deleted before the agent pool is removed.

**AKS authentication**: the cluster has `disableLocalAccounts: true`. Infrastructure fetches the kubeconfig from AKS ARM, then replaces the kubelogin exec plugin with `AksTokenProvider`, which acquires Entra ID tokens from the registered `TokenCredential` (shared with `ArmClient`). Falls back to `DefaultAzureCredential` unless `TenantId`/`ClientId`/`ClientSecret` are all set (service principal path). Config section: `NodeService:Aks`.

## Kernel proxy

`KubernetesRuntimeClient` tunnels all kernel API calls through the Kubernetes API server HTTP proxy: `/api/v1/namespaces/default/pods/{pod}:8000/proxy/{path}`. No Kubernetes Service, public IP, or VNet access is required — the K8s API server acts as the transport.

## Inactivity eviction

`InactivityEvictionService` (a `BackgroundService`) runs on a configurable interval and:
1. Deletes idle kernels whose `LastActivity` exceeds `KernelIdleTimeout` (default 10 min)
2. Stops nodes with no remaining kernels whose last kernel activity exceeds `NodeIdleTimeout` (default 20 min)

Configured via `InactivityEviction` section. Set `Enabled: false` to disable.

## API

Minimal API routes in `NodeEndpoints.MapNodeEndpoints()`. Pattern: node CRUD + stop/start at `/nodes/{name}`; kernel CRUD + execute/poll/restart/interrupt/completions/diagnostics at `/nodes/{name}/kernels/{kernelId}`.

Kernel execution is **async/poll**: `POST .../execute` returns HTTP 202 + `ExecutionHandle`; poll `GET .../executions/{executionId}` until `IsComplete`.

## Infrastructure (src/infra)

Bicep deploys AKS cluster, VNet, and ACR:
- Two user-assigned managed identities: cluster control plane + kubelet identity (shared by all node pools)
- Kubelet identity has `AcrPull` on ACR — no image pull secrets needed
- `disableLocalAccounts: true` + Azure RBAC on the cluster
- The API principal needs three roles on the cluster: **AKS Contributor** (node pool management), **AKS Cluster User** (fetch kubeconfig), **AKS RBAC Cluster Admin** (data-plane access)
- Deployment outputs: `acrLoginServer` -> `NodeService:Aks:RuntimeImage`; `nodeSubnetId` -> `NodeService:Aks:NodeSubnetId`
