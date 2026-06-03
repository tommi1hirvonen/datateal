# Local Development Setup

This guide covers everything you need to get Datateal running on your local machine using Docker Desktop Kubernetes and .NET Aspire.

---

## Prerequisites

| Tool                                                              | Version    | Purpose                                                                  |
| ----------------------------------------------------------------- | ---------- | ------------------------------------------------------------------------ |
| [Docker Desktop](https://www.docker.com/products/docker-desktop/) | v4+        | Runs runtime pods locally via the built-in Kubernetes cluster            |
| [.NET 10 SDK](https://dotnet.microsoft.com/download)              | 10         | All ASP.NET Core services and the Aspire app host                        |
| [Python](https://www.python.org/downloads/)                       | 3.11+      | Building and packaging the runtime service                               |
| [PostgreSQL](https://www.postgresql.org/download/)                | any recent | DuckLake catalog metadata storage                                        |
| Microsoft Entra ID                                                | —          | OIDC authentication for the UI server (app registration + client secret) |

**Docker Desktop Kubernetes** must be enabled in Docker Desktop → Settings → Kubernetes → Enable Kubernetes. The control plane uses the `docker-desktop` kubeconfig context and creates pods with `ImagePullPolicy: Never`, so the runtime image must be built locally (see [Build and deploy the runtime](#4-build-and-deploy-the-runtime)).

**PostgreSQL** is needed for DuckLake catalog metadata. This is separate from the two application databases (`datateal-control-plane` and `datateal-ui`) that Aspire provisions automatically as a local container. You can run Postgres in Docker:

```sh
docker run -d --name postgres-local -e POSTGRES_PASSWORD=yourpassword -p 5432:5432 postgres:17
```

---

## 1. Create the data directory

Runtime pods mount a host directory for DuckLake Parquet files. Create the directory before starting the stack — Kubernetes `hostPath` volumes do not create missing directories.

**Windows:**
```powershell
mkdir C:\Users\<you>\data\ducklake
```

**macOS / Linux:**
```sh
mkdir -p ~/data/ducklake
```

The control plane configuration references this path in a format that the Docker Desktop VM can resolve. The exact format differs by OS:

| OS | Host path | `DataVolumeHostPath` in config |
| -- | --------- | ------------------------------ |
| Windows | `C:\Users\<you>\data\ducklake` | `/run/desktop/mnt/host/c/Users/<you>/data/ducklake` |
| macOS | `/Users/<you>/data/ducklake` | `/Users/<you>/data/ducklake` |

Keep this in mind when filling in the `DataVolumeHostPath` setting below.

---

## 2. Create appsettings.Development.json files

All three service projects exclude `appsettings.Development.json` from source control (it is listed in each project's `.gitignore`). Create each file manually using the templates below. Values marked `# ← fill in` require your specific credentials or paths.

### Control plane

**`src/control-plane/Datateal.ControlPlane/appsettings.Development.json`**

```json
{
  "NodeService": {
    "Backend": "Local",
    "Local": {
      "KubeContext": "docker-desktop",
      "DataVolumeHostPath": "/run/desktop/mnt/host/c/Users/<you>/data/ducklake",
      "DataVolumeMountPath": "/data/ducklake"
    }
  },
  "ServiceAuth": {
    "ExpectedApiKey": "dev_key",
    "Runtime": {
      "ApiKey": "dev_key"
    }
  }
}
```

- `DataVolumeHostPath` — the path to the directory you created in step 1, in the format the Docker Desktop VM can resolve (see the table in step 1).
- `DataVolumeMountPath` — the path inside pods where the volume is mounted. Must match `Catalogs:BaseDataPath` in the orchestrator and UI server.
- `ServiceAuth:ExpectedApiKey` — the key that the orchestrator and UI server must send with every request to the control plane. Any string works for local development; keep it consistent across services.

### Orchestrator

**`src/orchestrator/Datateal.Orchestrator/appsettings.Development.json`**

```json
{
  "Catalogs": {
    "BaseDataPath": "/data/ducklake",
    "StorageConnectionString": "",
    "CatalogHost": "host.docker.internal",
    "CatalogPodHost": "",
    "CatalogPort": 5432,
    "CatalogUser": "postgres",
    "CatalogPassword": "<your postgres password>"
  },
  "ServiceAuth": {
    "ExpectedApiKey": "dev_key",
    "ControlPlane": { "ApiKey": "dev_key" }
  }
}
```

- `Catalogs:BaseDataPath` — must match `DataVolumeMountPath` in the control plane config.
- `Catalogs:CatalogHost` — set to `host.docker.internal` so that runtime pods (running inside Docker Desktop) can reach the Postgres server on the host machine.
- `ServiceAuth:ExpectedApiKey` — the key that the UI server must send with every request to the orchestrator.
- `ServiceAuth:ControlPlane:ApiKey` — must equal `ServiceAuth:ExpectedApiKey` in the control plane config.

### UI server

**`src/ui/server/Datateal.Ui.Server/appsettings.Development.json`**

```json
{
  "Catalogs": {
    "BaseDataPath": "/data/ducklake",
    "StorageConnectionString": "",
    "CatalogHost": "localhost",
    "CatalogPodHost": "host.docker.internal",
    "CatalogPort": 5432,
    "CatalogUser": "postgres",
    "CatalogPassword": "<your postgres password>"
  },
  "Authentication": {
    "Provider": "EntraId",
    "EntraId": {
      "Instance": "https://login.microsoftonline.com/",
      "TenantId": "<your tenant id>",
      "ClientId": "<your app registration client id>",
      "ClientSecret": "<your client secret>"
    }
  },
  "Authorization": {
    "AdminUsers": ["<your email>"]
  },
  "ServiceAuth": {
    "Orchestrator": { "ApiKey": "dev_key" },
    "ControlPlane": { "ApiKey": "dev_key" }
  }
}
```

- `Catalogs:CatalogHost` — `localhost` because the UI server process runs on the host and reaches Postgres directly.
- `Catalogs:CatalogPodHost` — `host.docker.internal` because runtime pods resolve the Postgres host from inside Docker Desktop.
- `Authentication:EntraId` — fill in the tenant ID, client ID, and client secret from your app registration (see [Entra ID app registration](#entra-id-app-registration) below).
- `Authorization:AdminUsers` — at least one email address that will have full admin access on first login, before any users are configured in the database.
- `ServiceAuth:Orchestrator:ApiKey` — must equal `ServiceAuth:ExpectedApiKey` in the orchestrator config.
- `ServiceAuth:ControlPlane:ApiKey` — must equal `ServiceAuth:ExpectedApiKey` in the control plane config.

### API key wiring summary

The services authenticate to each other using shared API keys. With the example configs above (all using `"dev_key"`), the wiring is:

```
UI server  ──(dev_key)──▶  Control plane  (ExpectedApiKey: "dev_key")
UI server  ──(dev_key)──▶  Orchestrator   (ExpectedApiKey: "dev_key")
Orchestrator ──(dev_key)──▶  Control plane  (ExpectedApiKey: "dev_key")
Control plane ──(dev_key)──▶  Runtime pods  (no server-side auth on the runtime)
```

Use any value you like for local development. Do not reuse development keys in production.

---

## 3. Entra ID app registration

You need a Microsoft Entra ID app registration for the UI server's OpenID Connect authentication. In the [Azure portal](https://portal.azure.com) → Entra ID → App registrations:

1. Create a new registration.
2. Add a redirect URI of type **Web**: `https://localhost:<ui-port>/signin-oidc` (the exact port is printed by Aspire when it starts the UI service).
3. Under **Certificates & secrets**, create a new client secret and copy the value immediately.
4. Copy the **Directory (tenant) ID** and **Application (client) ID**.
5. Fill in `TenantId`, `ClientId`, and `ClientSecret` in the UI server `appsettings.Development.json`.

---

## 4. Build and deploy the runtime

The control plane creates pods from `datateal-runtime:latest` with `ImagePullPolicy: Never` — meaning Kubernetes will only use a locally available image and never attempt to pull from a registry. You must build and tag the image before the stack can provision runtime nodes.

All commands run from `src/runtime/`.

**Create and activate a virtual environment:**

Windows:
```powershell
py -m venv .venv
.\.venv\Scripts\activate
```

macOS / Linux:
```sh
python3 -m venv .venv
source .venv/bin/activate
```

**Install the runtime package and its dependencies:**

```sh
pip install .
```

**Install the build tool:**

```sh
pip install build
```

**Build the wheel package:**

Windows:
```powershell
py -m build --wheel
```

macOS / Linux:
```sh
python3 -m build --wheel
```

> The Python version used to build the wheel should match the Python version in the Docker image (`python:3.14-slim` at the time of writing). Mismatches can cause import errors at runtime.

**Build the Docker image:**

```sh
docker build -t datateal-runtime .
```

The image is immediately available in Docker Desktop as `datateal-runtime:latest`. Repeat this step whenever you modify the runtime source code.

---

## 5. Run the stack

With Docker Desktop Kubernetes running and Postgres accessible, start the full stack from the repository root:

```sh
dotnet run --project src/app-host/Datateal.AppHost
```

Aspire starts all three .NET services with hot reload. The Aspire dashboard URL is printed to the console (typically `http://localhost:15888`) and gives you live logs, traces, and resource health for every service.

Alternatively, open `Datateal.slnx` in Visual Studio or JetBrains Rider and run the `Datateal.AppHost` project.

> **First run**: Aspire will start a local Postgres container for the application databases and run EF Core migrations automatically. The DuckLake catalog Postgres (configured in `Catalogs:CatalogHost`) is separate and must already be running.

> **Migration race on first start**: the orchestrator and UI server both run EF Core migrations against the shared `datateal-ui` database at startup. If they start at exactly the same time, one of them may fail with a database lock or migration error. This is harmless — simply stop and re-run the app host and all services will start cleanly on the second attempt.
