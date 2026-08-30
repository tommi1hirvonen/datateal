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

1. **Plan** (`/plan`) — dry-run. Validates the bundle (required fields, cross-references) and computes the full change set without modifying anything.
2. **Apply** (`/apply`) — validates the same rules, then executes the changes and returns the change set.

Running plan before apply is recommended but not required — both steps perform the same validation. Plan catches problems such as:

- Missing required fields (e.g. `vm_size` on a node pool, `node_pool_ref` on a notebook task)
- Broken cross-references (e.g. a job task referencing a node pool not in the bundle or workspace)
- Inconsistencies that would fail at runtime (e.g. a task dependency naming a task that doesn't exist in the same job)

All validation errors are collected and reported together so a single plan run surfaces the complete list.

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

- `${var.NAME}` — resolved from the `variables` map in `manifest.yml`. Use for non-sensitive, environment-specific values (paths, region names, feature flags).
- `${env.NAME}` — resolved from the `env` dictionary passed in the deployment request. Use for secrets that should never appear in source control.

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
  value: ${env.DB_PASSWORD}    # caller supplies DB_PASSWORD in the deploy request
```

---

## Secrets in deployments

### How `${env.NAME}` works

`${env.NAME}` tokens are resolved from the `env` JSON object passed as a form field alongside the bundle ZIP. This means secret values stay in your CI/CD system (GitHub Actions secrets, Azure Key Vault, etc.) and are never stored in source control or on the Datateal server.

**Passing env vars in the request (multipart form):**

```bash
curl -X POST ".../deployment/apply" \
  -H "Authorization: $TOKEN" \
  -F "bundle=@bundle.zip" \
  -F 'env={"DB_PASSWORD":"secret","PARTNER_API_KEY":"key123"}'
```

If no `env` field is provided (raw ZIP body or form without `env`), any `${env.NAME}` token in the bundle will cause a validation error at plan/apply time.

### Workspace secrets (`secrets.yml`)

Workspace secrets are sensitive values injected into notebook and query kernel environments as `os.environ['KEY']`. They are encrypted at rest.

```yaml
# resources/environment/secrets.yml

- key: db_password
  value: ${env.DB_PASSWORD}

- key: api_key_partner
  value: ${env.PARTNER_API_KEY}
```

**Behaviour by case:**

| Scenario                                | What happens                                             |
| --------------------------------------- | -------------------------------------------------------- |
| Secret already exists, `value` omitted  | NoChange — existing encrypted value is preserved         |
| Secret already exists, `value` provided | Updated with the new value                               |
| New secret, `value` provided            | Created with the provided value                          |
| New secret, `value` omitted             | **Plan/apply error** — value is required for new secrets |

Secrets are **never exported** — the export endpoint returns the key and description only, with the value omitted.

### Unmanaged catalog connection credentials

All unmanaged catalog fields support `${var.NAME}` and `${env.NAME}` substitution. The `catalog_password` field is a bundle field and follows the same semantics as workspace secrets:

| Scenario                                      | Behaviour                                                              |
| --------------------------------------------- | ---------------------------------------------------------------------- |
| New catalog, `catalog_password` provided      | Created with the provided password                                     |
| New catalog, `catalog_password` omitted       | **Plan/apply error** — password is required for new unmanaged catalogs |
| Existing catalog, `catalog_password` provided | Password updated to the provided value                                 |
| Existing catalog, `catalog_password` omitted  | Password left unchanged                                                |

`catalog_password` is **never exported** — the export endpoint omits it entirely.

```yaml
# resources/catalogs/partner_data.catalog.yml
type: unmanaged
name: partner_data
catalog_host: ${env.PARTNER_DB_HOST}
catalog_database: ducklake_partner
catalog_user: datateal_reader
# Required when creating a new catalog. Omit on subsequent deploys to preserve the stored password.
catalog_password: ${env.PARTNER_DB_PASSWORD}
data_path: abfss://partner@${var.adls_account}.dfs.core.windows.net/ducklake
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

**Catalog YAML** supports an optional `workspace_access` list that restricts which workspaces can attach the catalog (only meaningful when `accessible_from_all_workspaces: false`):

```yaml
type: unmanaged
name: partner_data
accessible_from_all_workspaces: false
workspace_access:
  - Sales (Prod)
  - Sales (Dev)
catalog_host: partner-db.internal
catalog_database: ducklake_partner
catalog_user: datateal_reader
catalog_password: ${env.PARTNER_DB_PASSWORD} # required for new catalogs; omit to preserve existing
data_path: abfss://partner@${var.adls_account}.dfs.core.windows.net/ducklake
```

**`catalog-access.yml`** is a flat list of per-user access entries — it controls which catalogs individual users can reach, independent of workspace access:

```yaml
- email: analyst@example.com
  has_all_catalog_access: false
  allowed_catalogs:
    - sales_prod
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
  notebooks/<path>.ipynb       # notebook source files (Jupyter nbformat 4)
  queries/<path>.sql           # SQL query files
files/
  wheels/<name>.whl            # wheel package binaries
```

---

## GitHub Actions example

Bundle secrets are passed as the `env` form field — a JSON object built from GitHub Actions secrets. No secrets are stored in the bundle or on the Datateal server.

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
            -H "Authorization: ${{ secrets.DATATEAL_TOKEN }}" \
            -F "bundle=@bundle.zip" \
            -F "env={\"DB_PASSWORD\":\"${{ secrets.DB_PASSWORD }}\",\"PARTNER_API_KEY\":\"${{ secrets.PARTNER_API_KEY }}\"}" \
            | jq .

      - name: Apply deployment
        run: |
          curl -sf -X POST "${{ vars.DATATEAL_URL }}/api/workspaces/${{ vars.WORKSPACE_ID }}/deployment/apply" \
            -H "Authorization: ${{ secrets.DATATEAL_TOKEN }}" \
            -F "bundle=@bundle.zip" \
            -F "env={\"DB_PASSWORD\":\"${{ secrets.DB_PASSWORD }}\",\"PARTNER_API_KEY\":\"${{ secrets.PARTNER_API_KEY }}\"}" \
            | jq .
```

If your bundle contains no `${env.*}` tokens you can omit the `env` field and send the ZIP directly:

```bash
curl -sf -X POST ".../deployment/apply" \
  -H "Authorization: $TOKEN" \
  -H "Content-Type: application/octet-stream" \
  --data-binary @bundle.zip | jq .
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
- Job YAML does **not** contain a `node_pools` section — node pool configs are separate workspace resources declared in `resources/node_pools/`. Job tasks reference them by name via `node_pool_ref`.
