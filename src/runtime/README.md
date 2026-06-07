## Datateal Runtime

Fast API runtime for Datateal. The API runs on each node in the Kubernetes cluster and manages the Python kernels.

### Development and build environment setup

Install [uv](https://docs.astral.sh/uv/getting-started/installation/) if not already installed.

```
curl -LsSf https://astral.sh/uv/install.sh | sh
```

Create a virtual environment and install the runtime package with all dependencies. Run from `src/runtime/`.

```
> uv sync
```

This creates `.venv/` and installs all dependencies from the lockfile. Activate the virtual environment when needed:

Windows:
```
> .\.venv\Scripts\activate
```

macOS / Linux:
```
> source .venv/bin/activate
```

Build the wheel package with uv.

```
> uv build --no-cache
```

The wheel package can then be installed in development mode.

```
uv pip install --editable .
```

### Testing

The project uses [pytest](https://docs.pytest.org/) with the [pytest-asyncio](https://pypi.org/project/pytest-asyncio/) plugin. Install the test dependencies first.

```
> uv sync --extra test
```

Run the full test suite from the `src/runtime` directory.

```
> python -m pytest tests/ -v
```

#### Test structure

| Module                            | Covers                                                                       |
| --------------------------------- | ---------------------------------------------------------------------------- |
| `tests/test_language_features.py` | Jedi semantic tokens, completions, hover documentation, pyflakes diagnostics |

Tests exercise the `KernelConnection` language-feature methods directly (no kernel process required). The current Python interpreter is used as the Jedi environment.

### Installation (Linux)

First install [uv](https://docs.astral.sh/uv/getting-started/installation/).

```
$ curl -LsSf https://astral.sh/uv/install.sh | sh
```

Then use `uv tool install` to install the built `.whl` (wheel) file.

```
uv tool install ./<file_name>.whl
```

### Installation (Windows)

First install [uv](https://docs.astral.sh/uv/getting-started/installation/).

```
> powershell -ExecutionPolicy ByPass -c "irm https://astral.sh/uv/install.ps1 | iex"
```

Then use `uv tool install` to install the built `.whl` (wheel) file.

```
> uv tool install ./<file_name>.whl
```

The Datateal runtime package depends on the duckdb Python package, which in turn requires [Microsoft Visual C++ Redistributable](https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist?view=msvc-170#latest-supported-redistributable-version) to be installed on the machine (Windows). See [this GitHub issue](https://github.com/duckdb/duckdb/issues/8101) for more context. Otherwise the runtime will fail during execution.

### Docker

The container image runs the API in an isolated Python environment and spins up kernels in a separate Python environment. Only `ipykernel` is required in the kernel environment; the API dependencies (FastAPI, uvicorn, etc.) are not available to kernels and vice versa.

#### Build the image

Build the wheel first with `uv build` (see [Development and build environment setup](#development-and-build-environment-setup)), then build the Docker image from the `src/runtime` directory.

```
docker build -t datateal-runtime .
```

The kernel environment comes with `ipykernel`, `duckdb`, `numpy` and `pandas` pre-installed. Additional packages can be supplied at runtime without rebuilding (see below).

#### Configuring kernel packages without rebuilding

Packages can also be installed in the kernel environment at container startup, without rebuilding the image. This is useful when different Kubernetes deployments need different packages but should share the same base image.

**Via environment variable** — set `KERNEL_PACKAGES` in the pod spec or `docker run` command.

```
docker run --rm -p 8000:8000 -e KERNEL_PACKAGES="numpy pandas" datateal-runtime
```

**Via a mounted requirements file** — mount a `requirements.txt` to `/etc/datateal/kernel-requirements.txt`. In Kubernetes this is typically done with a ConfigMap.

```yaml
# configmap.yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: kernel-requirements
data:
  requirements.txt: |
    requests
```

```yaml
# deployment.yaml (relevant excerpt)
volumeMounts:
  - name: kernel-requirements
    mountPath: /etc/datateal
volumes:
  - name: kernel-requirements
    configMap:
      name: kernel-requirements
      items:
        - key: requirements.txt
          path: kernel-requirements.txt
```

Both mechanisms can be used together. The requirements file is processed first, followed by `KERNEL_PACKAGES`. Note that installing packages on startup adds to the container's startup time.

#### Run locally with Docker Desktop

After building, the image is immediately available in Docker Desktop. Run it locally to verify.

```
docker run --rm -p 8000:8000 datateal-runtime
```

The API will be available at `http://localhost:8000`. Interactive API docs are at `http://localhost:8000/docs`.

#### Publish to Azure Container Registry (ACR)

Log in to your ACR instance. Replace `<registry>` with your registry name.

```
az acr login --name <registry>
```

Tag the local image with the fully qualified ACR repository name.

```
docker tag datateal-runtime <registry>.azurecr.io/datateal-runtime:<tag>
```

For example:

```
docker tag datateal-runtime acrdatatealdev.azurecr.io/datateal-runtime:latest
```

Push the image.

```
docker push <registry>.azurecr.io/datateal-runtime:<tag>
```

Reference the image in a Kubernetes pod spec or Helm chart.

```yaml
image: <registry>.azurecr.io/datateal-runtime:<tag>
```

Make sure the Kubernetes nodes have pull access to the registry. For AKS this is typically done by attaching the ACR to the cluster. This is automatically done by the Bicep template in the infra folder of this repository.

```
az aks update --name <aks-cluster> --resource-group <resource-group> --attach-acr <registry>
```
