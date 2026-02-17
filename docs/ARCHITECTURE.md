# Architecture: Secure File Transfer (my-app)

## Overview
A cloud-native secure file transfer system hosted in Azure that replaces a
legacy SFTP solution. Org users and third parties exchange flat files (PDF,
Excel, CSV) through a React web portal and REST API, authenticated via
Azure Entra ID, routed through Azure APIM, and stored in Azure Blob Storage
with per-container RBAC.

## System Architecture

```
                          ┌─────────────────────────────┐
                          │       Azure Entra ID         │
                          │  (Identity & Access Mgmt)    │
                          │                              │
                          │  - Org user accounts         │
                          │  - Third-party guest accts   │
                          │  - App registrations (sft-   │
                          │    3p-<company>) with certs   │
                          │  - RBAC role assignments      │
                          └──────────┬──────────────────┘
                                     │ OAuth2 tokens
                                     ▼
┌──────────────┐          ┌─────────────────────────────┐
│  React SPA   │─────────▶│     Azure APIM              │
│  (Portal)    │  HTTPS   │     (Consumption Tier)      │
└──────────────┘          │                              │
                          │  - JWT validation            │
┌──────────────┐          │  - Rate limiting             │
│  Third-Party │─────────▶│  - Request routing           │
│  Automation  │  HTTPS   │  - API versioning            │
│  (Scripts)   │          │  - CORS policy               │
└──────────────┘          └──────────┬──────────────────┘
                                     │
                                     ▼
                          ┌─────────────────────────────┐
                          │     .NET API                 │
                          │     (App Service)            │
                          │     [Access Restricted to    │
                          │      APIM IPs only]          │
                          │                              │
                          │  - Independent JWT validation│
                          │  - File upload/download      │
                          │  - File validation (magic    │
                          │    bytes + extension)        │
                          │  - Container listing         │
                          │  - Activity logging          │
                          │  - Managed Identity for      │
                          │    blob access (per-container)│
                          └──────┬──────┬───────┬───────┘
                                 │      │       │
                    ┌────────────┘      │       └────────────┐
                    ▼                   ▼                    ▼
     ┌───────────────────────────────────────────────────┐
     │            App Storage Account                    │
     │            (Keys DISABLED — Entra ID only)        │
     │                                                   │
     │  Blob Containers     Table Storage   Queue        │
     │  ┌─────────────────┐ ┌───────────┐  ┌──────────┐ │
     │  │ sft-<company>   │ │ Activity  │  │Provision │ │
     │  │  ├── inbound/   │ │ Registry  │  │ Queue    │ │
     │  │  └── outbound/  │ │ Notif.    │  └─────┬────┘ │
     │  └─────────────────┘ └───────────┘        │      │
     │  Soft-delete: 14 day                      │      │
     │  Lifecycle: Hot→Cold                      │      │
     └──────────┬────────────────────────────────┼──────┘
                │ Blob events                    │
                ▼                                ▼
     ┌──────────────────────┐  ┌────────────────────────────────┐
     │  Azure Event Grid    │  │  Function App (Consumption)    │
     │                      │  │  ┌───────────┐ ┌────────────┐ │
     │  - BlobCreated event │─▶│  │ Notify    │ │ Provision  │ │
     │  - Azure AD auth     │  │  │ Function  │ │ Function   │ │
     └──────────────────────┘  │  │           │ │            │ │
                               │  │ Identity: │ │ Identity:  │ │
                               │  │ id-sft-   │ │ id-sft-    │ │
                               │  │ func-     │ │ func-      │ │
                               │  │ notify    │ │ provision  │ │
                               │  └─────┬─────┘ └────────────┘ │
                               └────────┼───────────────────────┘
                                        │
                                        ▼
                               ┌────────────────────────────┐
                               │  Azure Communication Svcs  │
                               │  (Email Delivery)          │
                               └────────────────────────────┘

     ┌────────────────────────────────┐
     │  Functions Runtime Storage     │
     │  (Keys ENABLED — runtime only) │
     │  No business data              │
     └────────────────────────────────┘
```

## Component Responsibilities

### React SPA (Frontend)
- **Hosting**: Azure Static Web Apps or Storage Account + CDN
- **Auth**: MSAL.js for Entra ID interactive login (Authorization Code + PKCE)
- **Pages**:
  - Dashboard: list of third parties and recent activity
  - File browser: view inbound/outbound files per third party (Hot tier only)
  - Upload: drag-and-drop file upload to a selected third party's container
  - Activity log: audit trail of uploads/downloads per container
  - Admin: onboard new third parties (admin role only)
- **State**: Minimal — most state lives server-side. Use React Query or
  TanStack Query for API data fetching and caching.

### Azure APIM (Consumption)
- **Auth policy**: Validates JWT tokens from Entra ID on every request
- **Rate limiting**: Per-subscription throttling to prevent abuse
- **Routing**: Forwards to .NET API backend
- **CORS**: Allows the React SPA origin
- **API versioning**: URL path versioning (e.g., `/v1/files/...`)

### .NET API (Backend)
- **Hosting**: Azure App Service (Linux, B1 tier to start)
- **Network restriction**: App Service Access Restrictions lock inbound traffic
  to APIM outbound IPs only — the API is not directly reachable from the internet
- **Auth (defense-in-depth)**:
  - **Layer 1 (APIM)**: APIM validates JWT before forwarding — rejects
    obviously bad requests at the gateway
  - **Layer 2 (API)**: The .NET API independently validates every JWT using
    `Microsoft.Identity.Web` (`AddMicrosoftIdentityWebApiAuthentication`).
    Validates issuer, audience, signature, and expiry. This ensures the API
    is secure even if APIM is bypassed or misconfigured.
  - User identity extracted from validated claims for authorization decisions
- **Blob access**: Uses **Managed Identity** (no keys) to interact with
  Azure Storage via Azure.Storage.Blobs SDK
- **Responsibilities**:
  - File CRUD (upload, download, list, delete)
  - File validation (extension allowlist + magic byte verification)
  - Activity logging (writes to metadata store)
  - Trigger onboarding via provisioning pipeline (see below)
  - Health check endpoint (minimal response, no internal details)

### Azure Storage Account
- **SKU**: Standard_LRS (locally redundant — sufficient for low volume)
- **Container naming**: `sft-<company-name>` (lowercase, alphanumeric + hyphens)
- **Virtual directories**: `inbound/` and `outbound/` per container
- **Access tier**: Hot (default), lifecycle policy transitions to Cool at 30
  days and Cold at 90 days
- **Auth**: Entra ID only — storage account keys disabled
- **Encryption**: Azure-managed keys (default encryption at rest)
- **Soft-delete**: Enabled for blobs (14-day retention) and containers
  (7-day retention) — provides recovery from accidental or malicious deletion
- **Container isolation (defense-in-depth)**:
  - The API's Managed Identity is assigned **Storage Blob Data Contributor
    scoped per container** (not account-wide). The provisioning pipeline
    adds a role assignment for the API identity each time a new container
    is created.
  - `ContainerAccessMiddleware` verifies the caller's identity against the
    third-party registry before allowing any blob operation
  - Both layers must agree — a middleware bug alone cannot expose data
    because the Managed Identity also lacks cross-container RBAC
  - All 403 responses are logged and monitored for anomalous patterns

### Metadata Store (Azure SQL or Table Storage)
- **Purpose**: Stores data that doesn't belong in blob metadata:
  - File activity log (who uploaded/downloaded what, when)
  - Third-party registry (company name, container, app registration ID, contacts)
  - Notification preferences (email recipients per container)
- **Choice**: Azure Table Storage for simplicity and cost (no relational needs).
  Upgrade to Azure SQL if query complexity grows.

### Provisioning Pipeline (Isolated Identity)
The main .NET API does **not** hold Graph API permissions. Onboarding is
handled by the Provisioning function within the shared Function App, using
its own **user-assigned managed identity** to minimize the blast radius of
an API compromise.

- **Trigger**: The .NET API writes a provisioning request to an **Azure
  Storage Queue** when an admin submits the onboarding form
- **Provisioning Function**: A queue-triggered function in the shared Function
  App. Uses a dedicated **user-assigned managed identity** with tightly-scoped
  permissions (separate from the notification function's identity):
  - `Application.ReadWrite.OwnedBy` on Microsoft Graph (can only manage
    app registrations it created — not tenant-wide)
  - `Storage Blob Data Contributor` scoped to the storage account (to create
    containers)
  - `Microsoft.Authorization/roleAssignments/write` scoped to the storage
    account (to assign per-container RBAC)
- **Workflow**:
  1. Create storage container `sft-<company>`
  2. Create app registration `sft-3p-<company>` (if automation enabled)
  3. Assign RBAC on the new container
  4. Write third-party record to metadata store
  5. Update provisioning status (admin polls the API for completion)
- **Rate limiting**: Queue-based processing naturally throttles provisioning.
  Admin endpoints additionally rate-limited to 5 requests/minute.

### Notification Pipeline (Isolated Identity)
- **Event Grid**: Subscribes to BlobCreated events on the storage account,
  filtered by container prefix. Uses Azure AD authentication for event delivery.
- **Notification Function**: A function in the shared Function App, using its
  own **user-assigned managed identity** (separate from provisioning). Triggered
  by Event Grid, validates event authenticity, looks up the container in the
  third-party registry, resolves email recipients, sends email via ACS. Email
  content is minimal — "A new file is available in the portal" with no file
  names, sizes, or direct links.
- **Azure Communication Services**: Delivers the notification email

### Function App Identity Model
The single Function App hosts both functions but uses **user-assigned managed
identities** to enforce least privilege:

| Function       | Identity                  | Permissions                              |
|----------------|---------------------------|------------------------------------------|
| Notification   | `id-sft-func-notify`      | Table Storage read, ACS email send       |
| Provisioning   | `id-sft-func-provision`   | Graph `Application.ReadWrite.OwnedBy`, Storage RBAC write, Table Storage write |

Each function explicitly binds to its own identity in code. Neither function
can use the other's permissions.

## Authentication Flows

### Flow 1: Org User (Interactive)
```
Browser → Entra ID (Auth Code + PKCE) → Access Token
Browser → APIM (Bearer token) → .NET API → Storage (Managed Identity)
```

### Flow 2: Third-Party Interactive User
```
Browser → Entra ID (Auth Code + PKCE, guest account) → Access Token
Browser → APIM (Bearer token) → .NET API → Storage (Managed Identity)
```

### Flow 3: Third-Party Automation
```
Script → Entra ID (client_credentials + certificate) → Access Token
Script → APIM (Bearer token) → .NET API → Storage (Managed Identity)
```

**Key point**: The .NET API accesses storage via its own Managed Identity, which
is scoped per-container (not account-wide). The caller's identity (from the
independently validated JWT) determines *which containers* they can access.
Two layers enforce isolation:
1. **Application layer**: `ContainerAccessMiddleware` checks the caller's
   identity against the third-party registry
2. **Azure RBAC layer**: The API's Managed Identity only has role assignments
   on containers it should access — a middleware bypass cannot reach other
   containers because the identity lacks permission

> **Architecture Decision**: The API proxies blob access rather than giving
> callers direct blob access. This provides a single enforcement point,
> consistent audit logging, and the ability to add business logic (file type
> validation, malware scanning, size limits) without changing the auth model.

## API Design

### Base URL
```
https://<apim-name>.azure-api.net/sft/v1
```

### Endpoints

#### File Operations
```
GET    /containers                        List containers the caller can access
GET    /containers/{name}/files?dir=inbound|outbound
                                          List files (Hot tier only)
GET    /containers/{name}/files/{filename}?dir=inbound|outbound
                                          Download a file
POST   /containers/{name}/files?dir=inbound|outbound
                                          Upload a file (multipart/form-data)
DELETE /containers/{name}/files/{filename}?dir=inbound|outbound
                                          Delete a file
```

#### Activity Log
```
GET    /containers/{name}/activity        Get activity log for a container
GET    /activity                          Get activity log across all containers
                                          (org users / admins only)
```

#### Admin — Third-Party Onboarding
```
GET    /admin/third-parties               List all registered third parties
POST   /admin/third-parties               Provision a new third party
GET    /admin/third-parties/{id}          Get third-party details
PUT    /admin/third-parties/{id}          Update third-party config
DELETE /admin/third-parties/{id}          Deprovision a third party
```

#### Health
```
GET    /health                            Health check (no auth required)
```

### Request/Response Shapes

#### POST /containers/{name}/files?dir=inbound
```
Request:  multipart/form-data { file: <binary> }
Response: 201 Created
{
  "fileName": "report-2026-02.pdf",
  "container": "sft-acme",
  "directory": "inbound",
  "uploadedBy": "user@org.com",
  "uploadedAt": "2026-02-10T14:30:00Z",
  "sizeBytes": 245760
}
```

#### GET /containers/{name}/files?dir=outbound
```
Response: 200 OK
{
  "container": "sft-acme",
  "directory": "outbound",
  "files": [
    {
      "fileName": "invoice-001.xlsx",
      "sizeBytes": 102400,
      "uploadedAt": "2026-02-09T10:00:00Z",
      "accessTier": "Hot"
    }
  ]
}
```

#### POST /admin/third-parties
```
Request:
{
  "companyName": "Acme Corp",
  "contactEmail": "admin@acme.com",
  "enableAutomation": true
}
Response: 201 Created
{
  "id": "tp-001",
  "companyName": "Acme Corp",
  "containerName": "sft-acme",
  "appRegistrationId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
  "status": "provisioning",
  "createdAt": "2026-02-10T14:30:00Z"
}
```

### Error Responses
```
{
  "error": {
    "code": "CONTAINER_NOT_FOUND",
    "message": "The specified container does not exist or you do not have access."
  }
}
```

Standard HTTP status codes:
- 400 Bad Request (invalid input, wrong file type)
- 401 Unauthorized (missing/invalid token)
- 403 Forbidden (valid token, no access to this container)
- 404 Not Found (container or file doesn't exist)
- 413 Payload Too Large (file exceeds 50MB)
- 500 Internal Server Error

## Data Flows

### Flow: Org User Uploads a File
```
1. Org user logs in via React SPA → Entra ID → access token
2. User selects third party "Acme" and drops a file
3. React SPA → POST /containers/sft-acme/files?dir=inbound
   (Bearer token + multipart file)
4. APIM validates JWT, enforces 50MB size limit, forwards to .NET API
5. .NET API independently validates JWT (issuer, audience, signature)
6. .NET API checks caller has access to sft-acme container
   (middleware + registry lookup)
7. .NET API validates file: extension allowlist + magic byte verification
8. .NET API sanitizes filename (strips path components, special chars)
9. .NET API uploads blob to sft-acme/inbound/{sanitized-filename}
   via Managed Identity (scoped to sft-acme container)
10. .NET API writes activity record to metadata store
11. .NET API returns 201 Created
12. Event Grid fires BlobCreated event
13. Azure Function validates event, resolves Acme's contact email
14. Azure Function sends minimal "New file available" email via ACS
```

### Flow: Third-Party Automation Downloads a File
```
1. Third-party script authenticates via client_credentials + cert
   → Entra ID → access token
2. Script → GET /containers/sft-acme/files?dir=inbound
   (Bearer token)
3. APIM validates JWT, forwards to .NET API
4. .NET API independently validates JWT
5. .NET API checks caller's service principal has access to sft-acme
   (middleware + registry lookup)
6. .NET API lists blobs in sft-acme/inbound/ (Hot tier only)
7. .NET API returns file list
8. Script → GET /containers/sft-acme/files/{filename}?dir=inbound
9. .NET API validates dir param is strict enum ("inbound"|"outbound")
10. .NET API downloads blob, streams to caller with headers:
    Content-Disposition: attachment
    X-Content-Type-Options: nosniff
11. .NET API writes activity record to metadata store
```

### Flow: Admin Onboards a New Third Party
```
1. Admin logs in via React SPA (must have admin role + MFA via
   Conditional Access)
2. Admin fills onboarding form: company name, contact email, etc.
3. React SPA → POST /admin/third-parties
4. .NET API validates input, writes provisioning request to
   Azure Storage Queue, returns 201 with status "provisioning"
5. Provisioning Function (separate identity) picks up the message:
   a. Create storage container "sft-<company>"
   b. Assign API Managed Identity → Storage Blob Data Contributor
      on the new container
   c. Create app registration "sft-3p-<company>" (if automation enabled)
   d. Assign third-party service principal → Storage Blob Data
      Contributor on the new container
   e. Write third-party record to metadata store
   f. Update provisioning status to "active"
6. Admin polls GET /admin/third-parties/{id} for completion
```

## Key Decisions

| Decision | Rationale | Date |
|----------|-----------|------|
| API proxies all blob access (no direct SAS tokens) | Single enforcement point for auth, logging, and business rules | 2026-02-10 |
| Azure Table Storage for metadata (not SQL) | No relational queries needed; lower cost and complexity | 2026-02-10 |
| Managed Identity for API → Storage (per-container) | Eliminates all storage keys; per-container scoping prevents cross-tenant access at the Azure layer | 2026-02-10 |
| Consumption tier APIM | Low volume, pay-per-call, no idle cost | 2026-02-10 |
| Event Grid + Function for notifications | Decoupled from API; no notification logic in the request path | 2026-02-10 |
| Hot-tier-only in portal UI | Lifecycle tiering provides soft archive without deletion | 2026-02-10 |
| App Registration per third party | Required for per-container RBAC scoping; manageable via IaC | 2026-02-10 |
| Container naming: sft-<company> | Follows Azure naming rules; prefix groups containers logically | 2026-02-10 |
| Independent JWT validation in API (not just APIM) | Defense-in-depth; API is secure even if APIM bypassed or misconfigured | 2026-02-10 |
| Isolated provisioning via user-assigned identity | API never holds Graph write permissions; single Function App, separate identities | 2026-02-10 |
| Two storage accounts (app + Functions runtime) | App storage keys disabled for security; Functions runtime requires keys | 2026-02-10 |
| Consolidated Function App with user-assigned identities | Fewer resources; least privilege maintained via separate identity per function | 2026-02-10 |
| Blob + container soft-delete enabled | Recovery window for accidental/malicious deletion; low cost | 2026-02-10 |
| File validation: extension + magic bytes | Prevents disguised executables from being stored and served | 2026-02-10 |

## Project Structure

```
my-app/
├── docs/                          Documentation
│   ├── ARCHITECTURE.md            This file
│   ├── FEATURE-BRIEF.md           Product requirements
│   ├── CONTRIBUTING.md            Dev workflow and conventions
│   ├── LESSONS-LEARNED.md         Incident log
│   └── ROADMAP.md                 Future plans
│
├── src/
│   ├── api/                       .NET Web API
│   │   ├── SecureFileTransfer.Api/
│   │   │   ├── Controllers/
│   │   │   │   ├── ContainersController.cs
│   │   │   │   ├── FilesController.cs
│   │   │   │   ├── ActivityController.cs
│   │   │   │   └── AdminController.cs
│   │   │   ├── Services/
│   │   │   │   ├── IBlobStorageService.cs
│   │   │   │   ├── BlobStorageService.cs
│   │   │   │   ├── IActivityService.cs
│   │   │   │   ├── ActivityService.cs
│   │   │   │   ├── IOnboardingService.cs
│   │   │   │   └── OnboardingService.cs
│   │   │   ├── Models/
│   │   │   │   ├── FileInfo.cs
│   │   │   │   ├── ActivityRecord.cs
│   │   │   │   └── ThirdParty.cs
│   │   │   ├── Middleware/
│   │   │   │   └── ContainerAccessMiddleware.cs
│   │   │   ├── Program.cs
│   │   │   └── appsettings.json
│   │   └── SecureFileTransfer.Api.Tests/
│   │       ├── Controllers/
│   │       └── Services/
│   │
│   ├── web/                       React SPA
│   │   ├── src/
│   │   │   ├── components/
│   │   │   ├── pages/
│   │   │   │   ├── Dashboard.tsx
│   │   │   │   ├── FileBrowser.tsx
│   │   │   │   ├── Upload.tsx
│   │   │   │   ├── ActivityLog.tsx
│   │   │   │   └── AdminOnboarding.tsx
│   │   │   ├── services/
│   │   │   │   ├── api.ts
│   │   │   │   └── auth.ts
│   │   │   ├── hooks/
│   │   │   ├── App.tsx
│   │   │   └── main.tsx
│   │   ├── package.json
│   │   └── vite.config.ts
│   │
│   └── functions/                 Azure Functions (single Function App)
│       ├── SecureFileTransfer.Functions/
│       │   ├── NotificationTrigger.cs   (Event Grid → email, id-sft-func-notify)
│       │   ├── ProvisioningWorker.cs    (Queue → resource creation, id-sft-func-provision)
│       │   ├── Program.cs              (Host config, identity bindings)
│       │   └── host.json
│       └── SecureFileTransfer.Functions.Tests/
│
├── infra/                         Infrastructure as Code
│   ├── main.bicep                 Root deployment template
│   ├── modules/
│   │   ├── storage-app.bicep      App storage (blobs, tables, queues) + lifecycle
│   │   ├── storage-func.bicep     Functions runtime storage (keys enabled)
│   │   ├── identity.bicep         User-assigned managed identities + RBAC
│   │   ├── apim.bicep             APIM instance + API definitions
│   │   ├── appservice.bicep       App Service + plan for .NET API
│   │   ├── functions.bicep        Function App + Consumption plan (both functions)
│   │   ├── staticwebapp.bicep     Static Web App for React SPA
│   │   ├── communication.bicep    Azure Communication Services + email domain
│   │   ├── eventgrid.bicep        Event Grid system topic + subscription
│   │   └── monitoring.bicep       App Insights + Log Analytics + alerts
│   └── scripts/
│       └── onboard-third-party.bicep   Per-third-party provisioning
│
├── scripts/
│   ├── verify.ps1                 Quality gates
│   └── verify.bat                 CMD wrapper
│
├── tasks/
│   └── TASKS.md                   Session tracking
│
├── CLAUDE.md                      Project config for Claude Code
└── CHANGELOG.md                   Version history
```

## Security Considerations

### Authentication (Defense-in-Depth)
- **APIM** validates JWT at the gateway (Layer 1)
- **.NET API** independently validates JWT via `Microsoft.Identity.Web` (Layer 2)
- App Service Access Restrictions lock API to APIM outbound IPs only
- Conditional Access + MFA required for admin-role users
- Token lifetime: short access tokens (15-30 min), sliding refresh (8-12 hr)

### Authorization (Container Isolation)
- API Managed Identity scoped **per-container** (not account-wide)
- `ContainerAccessMiddleware` verifies caller identity against registry
- Both layers must agree — middleware bug alone cannot cross containers
- All 403 responses logged and monitored for anomalous patterns

### Data Protection
- Storage account keys **disabled** — all access via Entra ID + Managed Identity
- Blob soft-delete (14 days) and container soft-delete (7 days) enabled
- Azure-managed encryption at rest (evaluate CMK if handling PII/PHI)
- HTTPS only — no HTTP endpoints
- Download responses forced: `Content-Disposition: attachment`,
  `X-Content-Type-Options: nosniff`

### Input Validation
- File type: extension allowlist (.pdf, .xlsx, .xls, .csv, .txt) +
  magic byte verification (content must match declared type)
- File size: enforced at APIM (50MB) and API level
- File names: sanitized server-side (strip path components, special chars)
- `dir` parameter: strict enum validation ("inbound" | "outbound"),
  never concatenated into blob paths
- Duplicate filenames: reject with 409 Conflict (prevents silent overwrites)

### Provisioning Security
- Main API holds **no Graph API permissions** — cannot create app registrations
- Provisioning function uses its own **user-assigned managed identity**
  (`id-sft-func-provision`), isolated from the notification function's identity
- Provisioning identity scoped to `Application.ReadWrite.OwnedBy` (not
  tenant-wide `Application.ReadWrite.All`)
- Admin endpoints rate-limited (5 requests/minute)
- Queue-based processing provides natural throttling

### Storage Account Separation
- **App Storage Account** (keys disabled): All business data — blob containers,
  Table Storage (metadata), Storage Queue (provisioning). Accessed exclusively
  via Entra ID + Managed Identities.
- **Functions Runtime Storage** (keys enabled): Azure Functions internal state
  only (trigger checkpoints, lease blobs). Contains no business data. A key
  leak on this account exposes nothing of value.

### Notification Security
- Event Grid uses Azure AD authentication for event delivery
- Notification Function validates event authenticity before processing
- Emails contain no file names, sizes, or direct links — only a prompt
  to check the portal
- Recipient changes require admin approval

### Monitoring & Audit
- All file operations logged to metadata store (upload, download, delete)
- All auth failures (401), authorization denials (403), and validation
  failures (400) logged to Application Insights
- Correlation ID (`X-Correlation-ID`) propagated from APIM through all
  downstream calls for incident investigation
- Certificate expiry monitoring: weekly check via Azure Function, alerts
  at 90/60/30 days before expiry
- CORS restricted to the React SPA origin

### SPA Security Headers
- `Content-Security-Policy`: restrict scripts to self + MSAL CDN
- `X-Frame-Options: DENY`
- `X-Content-Type-Options: nosniff`
- `Referrer-Policy: strict-origin-when-cross-origin`

### Deprovisioning Checklist
When a third party is removed (`DELETE /admin/third-parties/{id}`):
1. Revoke and delete the App Registration
2. Remove all RBAC assignments on the container
3. Soft-delete the container (retained per soft-delete policy for compliance)
4. Disable notification subscriptions for the third party
5. Log the deprovisioning event to metadata store

### Future Considerations (Roadmap)
- Azure Front Door + WAF for OWASP protection and DDoS mitigation
- Microsoft Defender for Storage (malware scanning on upload)
- Upgrade to APIM Standard with VNet integration
- Azure Purview / DLP scanning for sensitive content classification
- Upgrade to GRS/RA-GRS if service becomes business-critical

## Azure Resource Inventory

### Base Resources (23)

**Identity & Access**

| # | Resource | Bicep Module | Purpose |
|---|----------|-------------|---------|
| 1 | Entra ID App Registration (SFT API) | identity.bicep | API scopes/roles for JWT validation |
| 2 | Entra ID App Registration (SFT SPA) | identity.bicep | React frontend MSAL login |
| 3 | Entra ID Security Groups | identity.bicep | Org user groups (SFT-Admins, SFT-Users) |
| 4 | Conditional Access Policy | identity.bicep | MFA for admin-role users |

**Compute**

| # | Resource | Bicep Module | Purpose |
|---|----------|-------------|---------|
| 5 | App Service Plan (B1 Linux) | appservice.bicep | Hosts the .NET API |
| 6 | App Service (Web App) | appservice.bicep | .NET API with access restrictions |
| 7 | App Service Plan (Consumption) | functions.bicep | Hosts the Function App |
| 8 | Function App | functions.bicep | Notification + Provisioning functions |
| 9 | Static Web App | staticwebapp.bicep | React SPA |

**Managed Identities**

| # | Resource | Bicep Module | Purpose |
|---|----------|-------------|---------|
| 10 | User-Assigned MI (id-sft-api) | identity.bicep | API → per-container blob, table, queue access |
| 11 | User-Assigned MI (id-sft-func-notify) | identity.bicep | Notification → table read + ACS send |
| 12 | User-Assigned MI (id-sft-func-provision) | identity.bicep | Provisioning → Graph + RBAC write + table write |

**Storage**

| # | Resource | Bicep Module | Purpose |
|---|----------|-------------|---------|
| 13 | Storage Account (App) | storage-app.bicep | Blobs + tables + queues (keys disabled) |
| 14 | Blob Lifecycle Management Policy | storage-app.bicep | Hot → Cool (30d) → Cold (90d) |
| 15 | Storage Account (Functions runtime) | storage-func.bicep | Functions internal state (keys enabled, no business data) |

**Networking & Gateway**

| # | Resource | Bicep Module | Purpose |
|---|----------|-------------|---------|
| 16 | API Management (Consumption) | apim.bicep | JWT validation, rate limiting, CORS, routing |

**Notifications**

| # | Resource | Bicep Module | Purpose |
|---|----------|-------------|---------|
| 17 | Event Grid System Topic | eventgrid.bicep | BlobCreated events on app storage |
| 18 | Event Grid Subscription | eventgrid.bicep | Routes events to notification function |
| 19 | Azure Communication Services | communication.bicep | Email service |
| 20 | ACS Email Domain | communication.bicep | Verified sender domain |

**Monitoring**

| # | Resource | Bicep Module | Purpose |
|---|----------|-------------|---------|
| 21 | Application Insights | monitoring.bicep | Distributed tracing, logging |
| 22 | Log Analytics Workspace | monitoring.bicep | Backing store, queries, alerting |
| 23 | Alert Rules | monitoring.bicep | 403 anomalies, cert expiry, provisioning failures |

### Per Third Party (4 resources, created by provisioning pipeline)

| # | Resource | Created By | Purpose |
|---|----------|-----------|---------|
| 24 | Blob Container (`sft-<company>`) | ProvisioningWorker | File storage with inbound/outbound dirs |
| 25 | App Registration + SP (`sft-3p-<company>`) | ProvisioningWorker | Automation auth (if enabled) |
| 26 | RBAC Assignment (third-party SP → container) | ProvisioningWorker | Storage Blob Data Contributor |
| 27 | RBAC Assignment (API identity → container) | ProvisioningWorker | Storage Blob Data Contributor |
