# API Design (Draft)

Base path points to the Go Function App. All endpoints require a valid Entra ID token (OIDC), with authorization driven by group claims and mappings.

## Core
- `GET /containers` — list containers the caller can access.
- `GET /browse?container={c}&path=/Inbound` — list directories/files under path; filter to Hot tier only.
- `POST /sas/read` — body `{ container, path }`; returns user delegation SAS URL with read.
- `POST /sas/write` — body `{ container, path }`; returns SAS URL with write/create.
- `DELETE /object` — body `{ container, path }`; delete if role permits (Modify/Full Control).

## Admin
- `GET /admin/config` — fetch limits/flags from App Config.
- `PUT /admin/config` — update limits/flags.
- `GET /admin/mappings` — list group → container/folder mappings.
- `PUT /admin/mappings` — upsert mapping entries.
- `GET /admin/audit` — query audit entries (paged/filtered by user/container/time).

## AuthZ Rules (roles)
- Full Control: list/read/write/create/delete.
- Read/Write: list/read/write/create.
- Modify: same as Full Control unless later differentiated.

## Audit
- Dual-write per operation: structured log to App Insights + row in `Audit` table (PartitionKey by container or user for efficient queries).
