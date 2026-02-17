# Secure File Transfer (my-app)

## Project Summary
A cloud-native secure file transfer system replacing legacy SFTP. Org users
and third parties exchange flat files (PDF, Excel, CSV) through a React web
portal and REST API, authenticated via Azure Entra ID, routed through Azure
APIM, stored in Azure Blob Storage with per-container RBAC.

## Tech Stack
- Language: C# (.NET 8), TypeScript (React)
- Backend: .NET Web API on Azure App Service
- Frontend: React SPA (Vite) on Azure Static Web Apps
- Storage: Azure Blob Storage (Entra ID auth, no keys)
- Gateway: Azure APIM (Consumption tier)
- Notifications: Azure Event Grid + Functions + Communication Services
- Metadata: Azure Table Storage
- IaC: Bicep
- Testing: xUnit (.NET), Vitest (React)

## Key Commands
```
dotnet build src/api/SecureFileTransfer.Api
dotnet test src/api/SecureFileTransfer.Api.Tests
cd src/web && npm run dev
cd src/web && npm run test
cd src/web && npm run lint
```

## Project Structure
```
src/api/       .NET Web API (controllers, services, models)
src/web/       React SPA (pages, components, services)
src/functions/ Azure Functions (notification trigger)
infra/         Bicep IaC templates
docs/          Architecture, feature brief, contributing guide
scripts/       Build/deploy/quality gate scripts
tasks/         Session-based task tracking
```

## Rules
- All new code must have tests.
- Files must not exceed 300 lines.
- Run lint and test before committing.
- Document architectural decisions in docs/ARCHITECTURE.md.
- Log mistakes in docs/LESSONS-LEARNED.md (append-only).

## Current Focus
- [ ] Scaffold .NET API project
- [ ] Scaffold React SPA project
- [ ] Create Bicep infrastructure templates
- [ ] Implement file upload/download endpoints
- [ ] Implement MSAL authentication in React
