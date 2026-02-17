# Feature Brief: Secure File Transfer (my-app)

## Problem Statement
Our organization exchanges flat files (PDF, Excel) with third parties. The
current SFTP-based solution lacks modern authentication, fine-grained access
control, and visibility into file activity. We need a cloud-native replacement
that is secure, auditable, and supports both interactive and automated workflows.

## Target Users
- **Org users**: Internal staff who upload/download files and manage third-party
  access through a web portal.
- **Third-party users**: External partners who push/pull files via the web portal
  or automated scripts (replacing SFTP service accounts).

## Proposed Solution
An Azure-hosted secure file transfer application with three components:

1. **Web Portal** — For org users to upload, download, and monitor file activity.
2. **REST API** — Behind Azure APIM for both portal and third-party automation.
3. **Notifications** — Email alerts when files are uploaded.

### Azure Infrastructure
| Component                    | Purpose                                        |
|------------------------------|------------------------------------------------|
| Azure Storage Account        | File storage (one container per third party)    |
| Azure Entra ID               | Identity provider for all users and services    |
| Azure APIM (Consumption)     | API gateway, auth enforcement, rate limiting    |
| Azure Event Grid / Functions | File event email notifications                  |
| Azure Communication Services | Email delivery for upload notifications         |

### Authentication Model
- **All authentication flows through Azure APIM** — no direct storage access.
- **No storage account keys** — all access via Entra ID identities and Azure RBAC.
- **Org users**: Entra ID accounts (interactive login via MSAL/OAuth2).
- **Third-party interactive users**: Entra ID accounts in our tenant.
- **Third-party automation**: One **App Registration per third party** with
  **client certificate** authentication (OAuth2 client_credentials flow).
  - Naming convention: `sft-3p-<company-name>`
  - Managed via IaC (Bicep/Terraform)
  - **Workload Identity Federation** available for third parties with their own
    IdP (eliminates certificates entirely).

### Access Control
- **Azure RBAC scoped per container** — no custom authorization code.
- Each third-party service principal and user group gets **Storage Blob Data
  Contributor** or **Storage Blob Data Reader** role on their specific container.
- Org users get broader RBAC across containers as needed.
- Azure provides a built-in audit trail for all access.

### File Constraints
- File types: PDF, Excel (.xlsx/.xls), flat files (CSV, TXT)
- File size: < 50MB (simple upload/download through API, no chunking needed)
- Volume: Low — no async processing or queuing required

### File Organization
- Each container uses **`inbound/`** and **`outbound/`** virtual directories
  - `inbound/` — files sent TO the third party (uploaded by org users)
  - `outbound/` — files received FROM the third party (uploaded by them)
- Mirrors the familiar SFTP directory convention

### Retention Strategy
- **Azure Blob Lifecycle Management** transitions files from **Hot** to
  **Cool/Cold** tier based on last modified date (configurable per policy)
- The web portal **only surfaces Hot tier files** to end users
- Files are never auto-deleted — they remain in Cold storage as an archive
- Reduces storage costs while preserving a full audit history

### Tech Stack
- **Backend API**: .NET (C#)
- **Frontend Portal**: React
- **API Gateway**: Azure APIM (Consumption tier)
- **Notifications**: Email via Azure Communication Services (or SendGrid)
- **IaC**: Bicep or Terraform

## User Stories

### Org Users
- As an org user, I want to **upload a file to a third party's container**
  through a web portal, so that I can share documents without using SFTP.
- As an org user, I want to **download files** that a third party has uploaded,
  so that I can receive their deliverables.
- As an org user, I want to **receive a notification** when a third party
  uploads a file, so that I don't have to manually check.
- As an org user, I want to **see a file activity log** for each third party,
  so that I can audit who uploaded/downloaded what and when.

### Third-Party Users (Interactive)
- As a third-party user, I want to **log in and upload/download files** through
  the web portal, so that I can exchange documents with the org.

### Third-Party Users (Automated)
- As a third-party system, I want to **authenticate via client certificate** and
  push/pull files via the API, so that I can automate file exchange without
  manual intervention.
- As a third-party system, I want to **poll the API for new files**, so that I
  can pull them automatically on a schedule.

### Administrators
- As an admin, I want to **provision a new third party** through an admin page
  in the portal (creates container, app registration, RBAC assignment), so that
  onboarding is self-service and auditable.

## Scope

### In Scope
- Web portal for org users (upload, download, activity view)
- REST API behind APIM (file CRUD operations)
- Entra ID authentication (interactive + client_credentials)
- Per-container RBAC via Azure native roles
- App registrations with client certificates for third-party automation
- Email notifications on file upload (via Azure Communication Services)
- Admin portal page for third-party onboarding (triggers IaC under the hood)
- Azure Blob Lifecycle Management (Hot → Cold tiering, portal shows Hot only)

### Out of Scope (for now)
- File transformation or processing (upload as-is, download as-is)
- File versioning (overwrite or unique naming — no version history)
- Direct blob access via SAS tokens (all access goes through the API)
- Multi-region replication
- Custom encryption beyond Azure's default encryption at rest
- Mobile app

## Resolved Decisions
| Question                | Decision                                         |
|-------------------------|--------------------------------------------------|
| Tech stack              | .NET API + React frontend                        |
| Notifications           | Email only (Azure Communication Services)        |
| Third-party onboarding  | Admin portal page (triggers IaC provisioning)     |
| File organization       | `inbound/` and `outbound/` per container          |
| Retention               | Lifecycle tiering (Hot → Cold), portal shows Hot  |
| APIM tier               | Consumption (pay-per-call)                        |

## Recommended Next Step
Engage the **Architect** agent to:
1. Define the system architecture and component diagram
2. Design the REST API contract (endpoints, request/response shapes)
3. Plan the IaC templates for Azure infrastructure provisioning
4. Define the data flow for upload, download, and notification paths
