# Architecture Overview

## Frontend
- Azure Static Web App (Standard) hosting SPA (React + Vite) with light/dark theme toggle.
- OIDC with Entra ID (PKCE), group claims drive RBAC; link to admin portal on sign-in page.
- Served at `files.credigy.com` via existing edge (LB + Palo Alto NAT + DNS).

## Backend
- Node.js Azure Function App (separate from SWA), VNet-integrated.
- Managed Identity with `Storage Blob Data Contributor` on ADLS Gen2.
- User delegation SAS issuance for read/write; no account keys.
- App Insights for telemetry; diagnostic settings to Log Analytics; dual-write audit to Table storage.
- Plan: Linux Function App on Premium (Elastic Premium) to support VNet integration to private endpoints.

## Data Plane
- ADLS Gen2 storage account with hierarchical namespace; per-vendor containers.
- Private Endpoints in dedicated subnet; private DNS `privatelink.blob.core.windows.net` (and DFS) linked to VNet.
- Hot-tier-only listings exposed to users; lifecycle rules handle retention/archival.

## Configuration and Audit
- Azure App Configuration for limits and feature flags.
- Table storage: `Mappings` (group -> container/folder), `Audit` (user activity).
- Admin portal manages mappings and limits; reads audits from Table storage.

## Networking
- VNet with subnets for Function integration and Private Endpoints; NSGs as needed.
- Function App outbound to storage via PE; inbound published through existing edge; CORS locked to `https://files.credigy.com`.

## CI/CD
- GitHub Actions with OIDC federation to Azure; SWA deploy + Function deploy (`frontend.yml`, `backend.yml`).
- No stored cloud secrets; only SWA token if not using built-in integration (SWA deploy token).

## Risks / Considerations
- Group claim overage: 86 vendors x 3 roles = 258 groups; this can exceed Entra group claim limits. Mitigate with app roles, group filtering, or Graph lookup when overage occurs.
- Frontend surface: SWA stays public; rely on OIDC + Conditional Access. Keep the API on a controlled domain (e.g., api.files.credigy.com) with CORS locked; ensure ingress is only through your edge.
- Private data plane: ADLS Gen2 uses both blob and dfs endpoints; create Private Endpoints for both and configure private DNS to keep traffic private.
- Backend reachability: SWA cannot call a purely private API; ensure the Function App endpoint is reachable from SWA (through your published edge) while keeping storage private via VNet + Private Endpoints.
- Service access to config/data: If you disable public network on App Config or Table storage, add Private Endpoints and make sure the Function’s VNet integration resolves them privately.
- Dual-write audit: App Insights + Table introduces eventual consistency; design admin queries to tolerate slight lag and partition keys for efficient reads.

## Planning To-Do
- Decide group claim strategy (app roles vs. filtered groups vs. Graph lookup fallback) and document mapping format in `Mappings`.
- Define API hostname and ingress path through your edge (e.g., `api.files.credigy.com`) and set expected CORS origins.
- Lock App Config key schema (e.g., `limits:maxUploadMB`, `limits:uploadTimeoutSec`, `feature:darkMode`) and Table partition keys for `Mappings`/`Audit`.
- Capture OIDC redirect URIs for SWA (prod/preview) and admin portal access rules (admin groups).
- Confirm both blob and dfs Private Endpoints + DNS entries will be created; same for App Config/Table if public access is disabled.
- Guest onboarding: Helpdesk creates Entra guest accounts, assigns correct vendor role groups, and sends the web link; no extra onboarding steps needed in the app if group claims are present.

## Function App Provisioning (manual steps)
- Create a **Function runtime storage account** (General Purpose v2, no hierarchical namespace) dedicated to the Function App; do not reuse the ADLS data plane account. Keep minimum TLS 1.2. If you later restrict public access, add Private Endpoints for blob/file/queue on this runtime account so the Function can read its own content/locks.
- Create a **Linux Function App** on **Premium (Elastic Premium)** in the target region; set `FUNCTIONS_WORKER_RUNTIME=node` and (optionally) `WEBSITE_NODE_DEFAULT_VERSION=~18`. Consumption does not support VNet integration to private endpoints. Link to the runtime storage account. Enable **System Assigned Managed Identity**.
- **VNet integration**: attach to the `function-integration` subnet. Ensure NSG allows outbound to the Private Endpoint subnet for storage/App Config/Table and to your edge for inbound path.
- **Private DNS**: ensure the VNet can resolve the ADLS blob/dfs endpoints (and App Config/Table if PE-protected). Add `privatelink.blob.core.windows.net` and `privatelink.dfs.core.windows.net` zones linked to the VNet; add records for the storage account.
- **RBAC**: grant the Function identity `Storage Blob Data Contributor` on the ADLS account, and Table Data roles on the account hosting `Mappings`/`Audit` tables. If App Config uses private access, grant App Config Data Reader.
- **App settings**: set CORS to `https://files.credigy.com` (and localhost for dev), configure App Config endpoint, and any table endpoints once defined.
- **Ingress**: publish the Function App via your edge (LB + Palo Alto/NAT) under `api.files.credigy.com` (or chosen hostname). SWA will call this public API endpoint; storage remains private via PE.
- **Validation**: from a VM in the VNet, curl the Function App endpoint and list the ADLS containers using the managed identity to confirm VNet integration and Private Endpoint path work. From the internet, storage endpoints should be unreachable.

## Function App Details (prod)
- Name/hostname: `func-credigyfiles-api-prod` (`func-credigyfiles-api-prod-e4b0fnbag9gafubh.eastus2-01.azurewebsites.net`)
- Region/plan: East US 2, Premium Elastic (EP)
- Runtime storage account: (GPv2 without HNS) — name pending
- Application Insights: `func-credigyfiles-api-prod` (East US 2)
- Managed Identity object ID (system assigned): `ab97fcea-4505-4718-9e51-846ed824d465`
