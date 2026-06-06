## Devcontainer for Codespaces

This devcontainer supports local development of Datateal inside GitHub Codespaces or VS Code Remote - Containers.

### Included tooling
- .NET 10 SDK
- Python 3.12
- `kubectl` and `minikube` (via `kubectl-helm-minikube` feature)
- Docker CLI + daemon (via `docker-in-docker` feature)
- PostgreSQL server (feature-installed, default password `datateal`)
- VS Code extensions: C#, Python, Docker, Kubernetes, and Postgres (`ms-ossdata.vscode-pgsql`)

### Overview
- The devcontainer starts Minikube (Docker driver) inside the container and mounts `${containerWorkspaceFolder}/data/ducklake` into the Minikube node at `/data/ducklake`.
- Run the Aspire AppHost from inside the container to start the services with hot reload.

### Required appsettings changes (Codespaces)

`postCreateCommand` runs `.devcontainer/setup-appsettings.sh` on first container creation, which writes all three `appsettings.Development.json` files with the correct Codespaces values. Each file is only written if it does not already exist, so any edits you make afterwards are preserved across rebuilds.

The generated defaults are shown below for reference. The only value that **must** be filled in manually is the Entra ID registration in the UI server config (see [CONTRIBUTING.md §3](../CONTRIBUTING.md#3-entra-id-app-registration)).

1) Control plane (`src/control-plane/Datateal.ControlPlane/appsettings.Development.json`)

Set the local node service data volume paths:

```json
"NodeService": {
    "Backend": "Local",
    "Local": {
        "KubeContext": "minikube",
        "DataVolumeHostPath": "/data/ducklake",
        "DataVolumeMountPath": "/data/ducklake"
    }
}
```

2) Orchestrator and UI (`src/orchestrator/Datateal.Orchestrator/appsettings.Development.json`, `src/ui/server/Datateal.Ui.Server/appsettings.Development.json`)

Use the local Postgres for service processes and `host.minikube.internal` for runtime pods:

```json
"Catalogs": {
    "BaseDataPath": "/data/ducklake",
    "StorageConnectionString": "",
    "CatalogHost": "localhost",
    "CatalogPodHost": "host.minikube.internal",
    "CatalogPort": 5432,
    "CatalogUser": "postgres",
    "CatalogPassword": "datateal"
}
```

3) Service auth keys (all services)

Use a shared development key across services, for example:

```json
"ServiceAuth": {
    "ExpectedApiKey": "dev_key",
    "ControlPlane": { "ApiKey": "dev_key" }
}
```

### Quick checks inside the devcontainer

```bash
dotnet --info
python --version
kubectl version --client
minikube status
psql -h localhost -U postgres -p 5432 -c "SELECT version();"
```

Postgres connection in VS Code
- Extension: `ms-ossdata.vscode-pgsql`
- Connection: host `localhost`, port `5432`, user `postgres`, password `datateal`.

### Development workflow

Follow [CONTRIBUTING.md](../CONTRIBUTING.md) for the full local development guide. When using this Codespaces devcontainer, apply the following exceptions:

- `appsettings.Development.json` files are scaffolded automatically on first container creation with the correct Codespaces values. You only need to fill in the Entra ID fields in `src/ui/server/Datateal.Ui.Server/appsettings.Development.json`.
- In `src/control-plane/Datateal.ControlPlane/appsettings.Development.json` the `KubeContext` is set to `minikube` (not `docker-desktop`) and the data volume paths are set to `/data/ducklake`.
- In the orchestrator and UI server config `CatalogHost` is `localhost` and `CatalogPodHost` is `host.minikube.internal` so that runtime pods reach the devcontainer Postgres.
- When building the runtime image, load it into minikube before starting the stack:

```bash
cd src/runtime
pip install . build
python3 -m build --wheel
docker build -t datateal-runtime .
minikube image load datateal-runtime:latest
cd ../..
dotnet run --project src/app-host/Datateal.AppHost
```

All other steps in CONTRIBUTING.md apply as written.

### Troubleshooting

If Minikube pods cannot reach the devcontainer Postgres via `host.minikube.internal`, create an ExternalName service in the cluster:

```bash
kubectl apply -f - <<'EOF'
apiVersion: v1
kind: Service
metadata:
  name: catalog-postgres
  namespace: default
spec:
  type: ExternalName
  externalName: host.minikube.internal
  ports:
    - port: 5432
EOF
```

Then set `CatalogPodHost` to `catalog-postgres.default.svc.cluster.local` in the orchestrator and UI server config.

If minikube fails to start, check `/tmp/minikube-start.log`. If the DuckLake mount is missing inside pods, check `/tmp/minikube-mount.log`.
