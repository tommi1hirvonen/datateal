#!/bin/bash
# setup-appsettings.sh — creates appsettings.Development.json files with
# Codespaces defaults. Each file is only written if it does not already
# exist, so any developer edits made after the first run are preserved.
set -euo pipefail

WORKSPACE_DIR=$(pwd)

write_if_missing() {
    local path="$1"
    local content="$2"
    if [ -f "$path" ]; then
        echo "  [skip] $path (already exists)"
    else
        mkdir -p "$(dirname "$path")"
        printf '%s\n' "$content" > "$path"
        echo "  [created] $path"
    fi
}

echo "Setting up appsettings.Development.json files..."

# ---------------------------------------------------------------------------
# Control plane
# ---------------------------------------------------------------------------
write_if_missing \
    "$WORKSPACE_DIR/src/control-plane/Datateal.ControlPlane/appsettings.Development.json" \
    '{
  "NodeService": {
    "Backend": "Local",
    "Local": {
      "KubeContext": "minikube",
      "DataVolumeHostPath": "/data/ducklake",
      "DataVolumeMountPath": "/data/ducklake"
    }
  },
  "ServiceAuth": {
    "ExpectedApiKey": "dev_key",
    "Runtime": {
      "ApiKey": "dev_key"
    }
  }
}'

# ---------------------------------------------------------------------------
# Orchestrator
# ---------------------------------------------------------------------------
write_if_missing \
    "$WORKSPACE_DIR/src/orchestrator/Datateal.Orchestrator/appsettings.Development.json" \
    '{
  "Catalogs": {
    "BaseDataPath": "/data/ducklake",
    "StorageConnectionString": "",
    "CatalogHost": "localhost",
    "CatalogPodHost": "host.minikube.internal",
    "CatalogPort": 5432,
    "CatalogUser": "postgres",
    "CatalogPassword": "datateal"
  },
  "ServiceAuth": {
    "ExpectedApiKey": "dev_key",
    "ControlPlane": { "ApiKey": "dev_key" }
  }
}'

# ---------------------------------------------------------------------------
# UI server
# Entra ID values must be filled in manually — see CONTRIBUTING.md §3 and
# .devcontainer/README.md for details.
# ---------------------------------------------------------------------------
write_if_missing \
    "$WORKSPACE_DIR/src/ui/server/Datateal.Ui.Server/appsettings.Development.json" \
    '{
  "Catalogs": {
    "BaseDataPath": "/data/ducklake",
    "StorageConnectionString": "",
    "CatalogHost": "localhost",
    "CatalogPodHost": "host.minikube.internal",
    "CatalogPort": 5432,
    "CatalogUser": "postgres",
    "CatalogPassword": "datateal"
  },
  "Authentication": {
    "Provider": "EntraId",
    "EntraId": {
      "Instance": "https://login.microsoftonline.com/",
      "TenantId": "",
      "ClientId": "",
      "ClientSecret": ""
    }
  },
  "Authorization": {
    "AdminUsers": []
  },
  "ServiceAuth": {
    "Orchestrator": { "ApiKey": "dev_key" },
    "ControlPlane": { "ApiKey": "dev_key" }
  }
}'

echo ""
echo "NOTE: Fill in Authentication.EntraId values in:"
echo "  src/ui/server/Datateal.Ui.Server/appsettings.Development.json"
echo "See CONTRIBUTING.md §3 (Entra ID app registration) for details."
