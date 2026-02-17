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

### Fixed
- Download endpoint returns 404 instead of 500 when blob not found
- OData filter injection risk in ActivityService and NotificationTrigger
- NotificationTrigger no longer falls back to placeholder sender/portal URLs
