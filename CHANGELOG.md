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

### Changed
- React SPA: Vite dev proxy (`/v1` → `localhost:5001`) for local API calls
- React SPA: `ApiError` class with structured error handling on all pages
- React SPA: MSAL `InteractionRequiredAuthError` triggers interactive token refresh
- React SPA: Dashboard shows Files/Upload/Activity action links per container
- React SPA: App shell with navbar, styled tables, forms, buttons, status badges
- React SPA: Replaced stock Vite template CSS with proper app layout

### Fixed
- Download endpoint returns 404 instead of 500 when blob not found
- OData filter injection risk in ActivityService and NotificationTrigger
- NotificationTrigger no longer falls back to placeholder sender/portal URLs
- React SPA: `erasableSyntaxOnly` TS error — explicit field declarations instead of parameter properties
- React SPA: React 19 `set-state-in-effect` lint errors — restructured useEffect patterns
