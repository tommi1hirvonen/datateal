# API Token Sample Requests

Use the [REST Client](https://marketplace.visualstudio.com/items?itemName=humao.rest-client) VS Code extension to run these `.http` files.

## Setup

1. Start the application (`dotnet run` or via Aspire).
2. Log in as an Admin user in the UI and navigate to **API Tokens** to generate the tokens below.
3. Fill in the variables at the top of each file before sending requests.

## Files

| File                    | Purpose                                                                              |
| ----------------------- | ------------------------------------------------------------------------------------ |
| `token-management.http` | Manage API tokens themselves (requires interactive Admin session or Admin API token) |
| `admin-token.http`      | Admin-scoped token: workspace CRUD, catalog management, user listing                 |
| `workspace-token.http`  | Workspace-scoped token: workspace items, environment variables, isolation checks     |
| `auth-failures.http`    | Expected 401/403 responses: no token, bad token, wrong scope, insufficient role      |

## Token types

| Type          | Scope            | Roles                                                                                                                                                                      |
| ------------- | ---------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Admin**     | Tenant-wide      | `Admin`, `CatalogContributor`                                                                                                                                              |
| **Workspace** | Single workspace | `WorkspaceAdmin`, `WorkspaceContributor`, `WorkspaceReader`, `NodePoolContributor`, `NodePoolOperator`, `JobContributor`, `JobOperator`, `JobReader`, `EnvironmentManager` |

## Authentication methods

Both methods are supported and tested here:

```
# Header
X-Datateal-Api-Token: dtl_...

# Bearer
Authorization: Bearer dtl_...
```
