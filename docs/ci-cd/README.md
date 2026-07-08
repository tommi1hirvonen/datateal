# Datateal CI/CD & Deployment Bundles

Datateal supports **declarative, version-controlled deployments** via deployment bundles — ZIP archives of YAML files that fully describe the desired state of your platform configuration or a single workspace. The same files that CI/CD pipelines deploy live in your git repository, enabling code-reviewed infrastructure changes, environment promotion, and two-way sync.

---

## Concepts

### Scopes

| Scope         | What it manages                                                                                              | Semantics                                                                                            | Required auth                              |
| ------------- | ------------------------------------------------------------------------------------------------------------ | ---------------------------------------------------------------------------------------------------- | ------------------------------------------ |
| **Admin**     | Catalogs, workspaces, memberships, catalog access grants                                                     | **Upsert-only** — resources present in the environment but absent from the bundle are left untouched | Admin API token                            |
| **Workspace** | Folders, notebooks, SQL queries, node pools, jobs, schedules, environment variables, secrets, wheel packages | **Full sync** — resources absent from the bundle are deleted                                         | Workspace API token with `WorkspaceManage` |

Admin deployments are intentionally non-destructive. To remove a catalog or workspace, do so through the UI — this is a safeguard against accidental wide-impact deletes in CI/CD.

### Plan → Apply

Every deployment goes through a two-step cycle:

1. **Plan** (`/plan`) — dry-run. Computes the full change set and returns it without modifying anything.
2. **Apply** (`/apply`) — executes the changes and returns the same change set with the actual results.

Both operations return a structured change set:

```json
{
  "scope": "workspace",
  "target": "Sales (Prod)",
  "dryRun": true,
  "changes": [
    {
      "resourceType": "notebook",
      "resourceName": "pipelines/ingest_sales",
      "changeType": "Update"
    },
    { "resourceType": "job", "resourceName": "nightly_etl", "changeType": "NoChange" },
    { "resourceType": "secret", "resourceName": "db_password", "changeType": "NoChange" }
  ],
  "summary": { "create": 0, "update": 1, "delete": 0, "noChange": 2 }
}
```

### Natural keys — no GUIDs

All resource YAML uses human-readable natural identifiers. GUIDs never appear in bundle files:

| Resource                     | Natural key                               |
| ---------------------------- | ----------------------------------------- |
| Catalog, Workspace           | `name`                                    |
| Notebook, Query              | full path (e.g. `pipelines/ingest_sales`) |
| Folder                       | full path (e.g. `etl/staging`)            |
| Node pool, Job, Schedule     | `name` within a workspace                 |
| Environment variable, Secret | `key`                                     |
| Wheel package                | `name`                                    |
| User                         | `email`                                   |
| Workspace membership         | workspace name + user email               |

This means the same bundle file deploys correctly to dev, test, and production — the workspace ID in the deploy URL is the only environment-specific value.

---

## Authentication

Use **API tokens** for CI/CD access — never interactive user credentials.

### Create an API token

1. Log in as an Admin and navigate to **Admin → API Tokens**.
2. Click **Create token**, choose the scope, assign roles, and optionally set an expiry.
3. Copy the token — it is displayed **once only**.

### Admin token

```bash
# Plan an admin bundle
curl -X POST https://datateal.example.com/api/deployments/admin/plan \
  -H "Authorization: Bearer dtl_<your-admin-token>" \
  -H "Content-Type: application/octet-stream" \
  --data-binary @admin-bundle.zip

# Apply
curl -X POST https://datateal.example.com/api/deployments/admin/apply \
  -H "Authorization: Bearer dtl_<your-admin-token>" \
  -H "Content-Type: application/octet-stream" \
  --data-binary @admin-bundle.zip
```

### Workspace token

```bash
WORKSPACE_ID="<workspace-guid>"

# Plan a workspace bundle
curl -X POST "https://datateal.example.com/api/workspaces/${WORKSPACE_ID}/deployment/plan" \
  -H "Authorization: Bearer dtl_<your-workspace-token>" \
  -H "Content-Type: application/octet-stream" \
  --data-binary @workspace-bundle.zip

# Apply
curl -X POST "https://datateal.example.com/api/workspaces/${WORKSPACE_ID}/deployment/apply" \
  -H "Authorization: Bearer dtl_<your-workspace-token>" \
  -H "Content-Type: application/octet-stream" \
  --data-binary @workspace-bundle.zip
```

### Export current state

Export downloads a bundle representing the current live state of an environment — useful for bootstrapping version control or inspecting what is deployed.

```bash
# Export workspace as bundle ZIP
curl -X GET "https://datateal.example.com/api/workspaces/${WORKSPACE_ID}/deployment/export" \
  -H "Authorization: Bearer dtl_<your-workspace-token>" \
  -o workspace-bundle.zip

# Export admin state
curl -X GET "https://datateal.example.com/api/deployments/admin/export" \
  -H "Authorization: Bearer dtl_<your-admin-token>" \
  -o admin-bundle.zip
```

---

## Variable substitution

Bundle YAML can include `${var.NAME}` and `${env.NAME}` tokens resolved at deploy time:

- `${var.NAME}` — resolved from the `variables` map in `manifest.yml`.
- `${env.NAME}` — resolved from the deploying process's environment variables (ideal for CI/CD secret injection).

```yaml
# manifest.yml
scope: workspace
target_workspace: Sales (Prod)
variables:
  data_path: abfss://data@mystorageaccount.dfs.core.windows.net/sales

# resources/environment/variables.yml
- key: DATA_PATH
  value: ${var.data_path}

# resources/environment/secrets.yml
- key: db_password
  value: ${env.DB_PASSWORD}    # injected from GitHub Actions secret
```

---

## Bundle format

A bundle is a **ZIP file** containing a specific directory layout. The root `manifest.yml` determines the scope; all other files are discovered by path convention.

### Admin bundle layout

```
manifest.yml
resources/
  catalogs/<name>.catalog.yml
  workspaces/<name>.workspace.yml
  permissions/
    memberships.yml
    catalog-access.yml
```

### Workspace bundle layout

```
manifest.yml
resources/
  node_pools/<name>.nodepool.yml
  jobs/<name>.job.yml
  environment/
    variables.yml
    secrets.yml
  wheel_packages/<name>.yml
  folders.yml
src/
  notebooks/<path>.py          # notebook source files
  queries/<path>.sql           # SQL query files
files/
  wheels/<name>.whl            # wheel package binaries
```

---

## GitHub Actions example

```yaml
name: Deploy Datateal workspace

on:
  push:
    branches: [main]
    paths: ['deploy/workspace-bundle/**']

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Create bundle ZIP
        run: cd deploy/workspace-bundle && zip -r ../../bundle.zip .

      - name: Plan deployment
        run: |
          curl -sf -X POST "${{ vars.DATATEAL_URL }}/api/workspaces/${{ vars.WORKSPACE_ID }}/deployment/plan" \
            -H "Authorization: Bearer ${{ secrets.DATATEAL_TOKEN }}" \
            -H "Content-Type: application/octet-stream" \
            --data-binary @bundle.zip | jq .

      - name: Apply deployment
        run: |
          curl -sf -X POST "${{ vars.DATATEAL_URL }}/api/workspaces/${{ vars.WORKSPACE_ID }}/deployment/apply" \
            -H "Authorization: Bearer ${{ secrets.DATATEAL_TOKEN }}" \
            -H "Content-Type: application/octet-stream" \
            -H "DB_PASSWORD=${{ secrets.DB_PASSWORD }}" \
            --data-binary @bundle.zip | jq .
        env:
          DB_PASSWORD: ${{ secrets.DB_PASSWORD }}
```

---

## Sample bundles

| Directory                                | Description                                                                                             |
| ---------------------------------------- | ------------------------------------------------------------------------------------------------------- |
| [`admin-bundle/`](admin-bundle/)         | Full admin-scope bundle: two catalogs, two workspaces, memberships, catalog access                      |
| [`workspace-bundle/`](workspace-bundle/) | Full workspace-scope bundle: node pool, job with two tasks, env vars, secrets, folders, sample notebook |

---

## YAML key conventions

- All keys are **snake_case** (`notebook_path`, `node_pool_ref`, `default_value`).
- Multi-word enum values are **snake_case** (`sql_query`, `sub_job`, `on_success`, `on_failure`).
- Role names keep their canonical PascalCase identifiers (`WorkspaceAdmin`, `JobContributor`).
- `null` / default values are omitted on export.
- REST/JSON API responses (change sets) use camelCase per standard JSON convention.
