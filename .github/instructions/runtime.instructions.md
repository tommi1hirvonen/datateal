---
applyTo: src/runtime/**
---

# DuckHouse Runtime

FastAPI service (`duckhouse_runtime` package) that runs on each Kubernetes node and manages Jupyter Python kernels. The control plane communicates with it via the Kubernetes API server HTTP proxy on port 8000.

## Architecture

Two isolated Python environments live in the Docker image:

| Environment | Path | Contents |
|---|---|---|
| API | `/opt/venvs/api` | FastAPI, uvicorn, pydantic, jupyter-client, ipykernel, jedi, pyflakes |
| Kernel | `/opt/venvs/kernel` | ipykernel, duckdb (+ optional extras) |

`DUCKHOUSE_KERNEL_PYTHON=/opt/venvs/kernel/bin/python` points the kernel manager at the kernel venv. This separation means API dependency changes never affect the kernel environment and vice versa.

**Kernel lifecycle**: `KernelRegistry` (module-level singleton) owns all running kernels and calls `shutdown_all()` on FastAPI lifespan shutdown. Each `KernelConnection` wraps a `jupyter_client` `AsyncKernelManager` + `AsyncKernelClient`. Executions are serialised per kernel via an `asyncio.Lock` but launched as background tasks — `start_execute()` returns an `execution_id` immediately (HTTP 202); the caller polls `get_execution()` until complete.

**DataFrame formatter**: On kernel start/restart, `_setup_formatters()` silently executes `dataframe_formatter.py` inside the kernel. This registers an IPython MIME formatter for `application/vnd.duckhouse.dataframe+json` that serialises `pandas.DataFrame` and `duckdb.DuckDBPyRelation` to structured JSON (columns, rows capped at 10 000, row counts).

**Code intelligence**: `complete()` uses Jedi (pointed at the kernel venv) for context-aware completions. `diagnose()` uses pyflakes + `compile()` for syntax and lint diagnostics. Both accept a `context` parameter containing prior-cell code.

## API

All endpoints are under `/kernels`. Key patterns:
- **Async execution**: `POST /kernels/{id}/execute` → HTTP 202 + `{"execution_id": "..."}`. Poll `GET /kernels/{id}/executions/{execution_id}` until `is_complete: true`.
- **Error codes**: 404 = kernel/execution not found, 408 = timeout, 409 = kernel dead.
- Standard CRUD on `/kernels`: create, list, get, delete.
- `/kernels/{id}/restart`, `/interrupt`, `/completions`, `/diagnostics`.

## Configuration

| Variable | Default | Description |
|---|---|---|
| `DUCKHOUSE_KERNEL_PYTHON` | `sys.executable` | Python executable for kernel subprocesses |
| `KERNEL_PACKAGES` | _(none)_ | Space-separated packages installed into kernel venv at startup |

A requirements file at `/etc/duckhouse/kernel-requirements.txt` is also installed into the kernel venv at startup (processed before `KERNEL_PACKAGES`).

## Docker

Build requires the wheel first (`py -m build --wheel`), then `docker build -t duckhouse-runtime .` from `src/runtime/`. Push to ACR and AKS pulls without credentials — the kubelet identity has `AcrPull` (granted by the Bicep template).

