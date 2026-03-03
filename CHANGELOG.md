# Changelog

## [Unreleased]
### Added
- Group-based authorization: `ContainerAccessResult` model (`None`, `ReadOnly`, `Full`)
- `UserGroupId`/`AdminGroupId` fields on `ThirdParty` entity, DTOs, and SPA forms
- `ActivityService.UserHasContainerAccessAsync` checks Entra ID group claims against ThirdParty registry
- `ContainerAccessExtensions.CheckContainerAccessAsync` extracts `groups` claim from JWT
- Delete permission gating: `FilesController.DeleteFile` returns 403 for ReadOnly users
- `DevAuthHandler` reads `Authentication:DevGroups` config for local group testing
- Admin UI: group ID fields in `AdminOnboarding.tsx` create form + table display
- 80 ThirdParty records populated from Entra ID groups via `scripts/populate-group-ids.py`
- APIM UDR route table (`rt-apim-dev-eus`) for VNet peering compatibility
- APIM subnet service endpoints: ServiceBus, AzureActiveDirectory (added to existing 4)
- RBAC module (`modules/rbac.bicep`) for managed identity → storage role assignments
- Initial project scaffold
- Local development support: Azurite connection string + DevAuthHandler for JWT bypass
- Quality gate script (`verify.ps1`) with dotnet build + test
- FileValidationServiceTests: directory validation, filename sanitization, magic bytes, extension allowlist
- FilesControllerTests: upload/download/delete pipeline, access control, file validation
- BlobStorageServiceTests: duplicate rejection, container existence, blob path construction
- OnboardingServiceTests: provisioning, deprovisioning, company name sanitization, queue integration
- ODataSanitizer utility (API + Functions) for safe OData filter string interpolation
- ODataSanitizerTests: escape quotes, injection attempts, empty strings
- FileAlreadyExistsException: typed exception replacing fragile string-matching catch
- Pagination (`top` query param) on all list endpoints (files, activity, third-parties)
- Dev MSAL bypass: `VITE_DEV_AUTH=true` skips token acquisition, shows DEV badge in navbar
- Local API development: Azurite on custom ports, Vite proxy, conditional App Insights/HTTPS
- Bicep environment integration: VNet, tags, cross-RG LAW, Flex Consumption Function App
- New `monitoring-law.bicep` sub-module for cross-resource-group LAW deployment
- Deployment container in func storage for Flex Consumption package deployment

### Changed
- Bicep: Function App identity changed to `SystemAssigned, UserAssigned` for deployment storage auth
- Bicep: Event Grid module is now conditional (`deployEventGrid` param, default false)
- Bicep: Entra ID placeholders parameterized in App Service (`aadTenantId`, `aadApiClientId`, `aadApiAudience`)
- Bicep: Entra ID values updated to real app registration IDs (tenant, API client, audience)
- CD: GitHub env vars for SPA build (`VITE_AAD_TENANT_ID`, `VITE_AAD_CLIENT_ID`, `VITE_AAD_API_CLIENT_ID`)
- Entra ID: API app registration configured (identifier URI, `access_as_user` scope, token v2, SPA pre-auth)
- Entra ID: SPA app registration configured (redirect URIs for SWA + localhost, API permission, admin consent)
- Bicep: LAW deploys in project RG (removed cross-RG dependency on rg-security-dev-eus)
- React SPA: Vite dev proxy (`/v1` → `localhost:5292`) for local API calls
- React SPA: `ApiError` class with structured error handling on all pages
- React SPA: MSAL `InteractionRequiredAuthError` triggers interactive token refresh
- React SPA: Dashboard shows Files/Upload/Activity action links per container
- React SPA: App shell with navbar, styled tables, forms, buttons, status badges
- React SPA: Replaced stock Vite template CSS with Credigy Files dark theme
- React SPA: Renamed app to "Credigy Files" throughout (index.html, App.tsx, Dashboard.tsx)
- Bicep: Function App switched from Consumption (Y1) to Flex Consumption (FC1)
- Bicep: App Service and Function App now VNet integrated via shared VNet subnets
- Bicep: Log Analytics Workspace deploys to `rg-security-dev-eus` (cross-RG)
- Bicep: All resources tagged with Environment, Project, Owner
- Bicep: AzureWebJobsStorage uses managed identity URIs instead of connection string
- Bicep: Static Web App uses `eastus2` (not available in `eastus`)
- Bicep: `environment()` function replaces hardcoded login.microsoftonline.com URL
- Bicep: MSAL auth placeholders use GUID format for Entra ID values

### Fixed
- CI: ESLint crash — removed `ajv`/`minimatch` overrides from `package.json` (ajv v8 broke `@eslint/eslintrc`)
- CD: API deploy 502 — increased propagation wait to 60s + 3-attempt retry with 30s backoff
- CD: Functions deploy 403 — open/close `sftcredigyfilesdevfunc` public network access around deploy
- API: 10+ minute startup — replaced `DefaultAzureCredential` with `ManagedIdentityCredential` in Azure
- APIM: VNet injection failures — NSG monitor port 1866→1886, UDR for peered VNet routing, missing service endpoints
- CI: cross-platform filename sanitization (backslash handling for Linux)
- CD: `azure/functions-action` changed from `@v2` (nonexistent) to `@v1`
- CD: Flex Consumption VNet workaround — auto-remove/restore VNet integration around Functions deploy
- CD: 60s propagation delay after VNet removal for Kudu connectivity
- CD: Fetch SWA deployment token dynamically via `az staticwebapp secrets list`
- Bicep: RBAC — added Queue/Table/AccountContributor roles for Function App system identity on func storage
- Bicep: API audience set to identifier URI (`api://...`) instead of bare client ID
- SPA: MSAL scopes aligned with Entra ID `access_as_user` scope (was `Files.ReadWrite`)
- Download endpoint returns 404 instead of 500 when blob not found
- OData filter injection risk in ActivityService and NotificationTrigger
- NotificationTrigger no longer falls back to placeholder sender/portal URLs
- React SPA: `erasableSyntaxOnly` TS error — explicit field declarations instead of parameter properties
- React SPA: React 19 `set-state-in-effect` lint errors — restructured useEffect patterns
- Bicep: Storage account `name =` syntax errors changed to `name:` in both storage modules
- Bicep: Removed unused `functionAppName` parameter from Event Grid module
- Local dev: App Insights disabled in Development (crashes without connection string)
- Local dev: HTTPS redirect disabled in Development (plain HTTP on port 5292)
