# Changelog

## [Unreleased]
### Added
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
- Download endpoint returns 404 instead of 500 when blob not found
- OData filter injection risk in ActivityService and NotificationTrigger
- NotificationTrigger no longer falls back to placeholder sender/portal URLs
- React SPA: `erasableSyntaxOnly` TS error — explicit field declarations instead of parameter properties
- React SPA: React 19 `set-state-in-effect` lint errors — restructured useEffect patterns
- Bicep: Storage account `name =` syntax errors changed to `name:` in both storage modules
- Bicep: Removed unused `functionAppName` parameter from Event Grid module
- Local dev: App Insights disabled in Development (crashes without connection string)
- Local dev: HTTPS redirect disabled in Development (plain HTTP on port 5292)
