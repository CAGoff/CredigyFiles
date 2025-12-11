# Credigy Files

Secure vendor file exchange using Azure Static Web Apps (frontend) and a Go-based Azure Function App (backend) with ADLS Gen2 storage kept private via VNet + Private Endpoints.

## Project Structure
- `api/` — Go custom handler for Azure Functions (HTTP endpoints, SAS issuance, authZ).
- `web/` — SPA frontend (SWA).
- `docs/` — Vision, architecture, API design.

## Prerequisites (dev)
- Go 1.21+
- Azure Functions Core Tools v4
- Node.js/Yarn (for `web/`, later)
- Azurite or storage emulator for local Functions (`AzureWebJobsStorage=UseDevelopmentStorage=true`)

## Running the API locally (stub, Node.js)
```bash
cd api
npm install
func start
```
Then GET `http://localhost:7071/api/health`.

## Running the web locally
```bash
cd web
npm install
npm run dev
```
Open the printed localhost URL (default Vite port 5173). The current SPA is a placeholder with light/dark toggle and copy for next steps.

## Config Samples
- `api/local.settings.sample.json` shows required settings. Copy to `local.settings.json` for local runs (keep it uncommitted).

## Notes
- Backend now uses Azure Functions with Node.js. CORS for the real Function App should be restricted to `https://files.credigy.com` in production; add localhost during development.

## CI/CD (GitHub Actions)
- Workflows: `.github/workflows/frontend.yml` (SWA deploy) and `.github/workflows/backend.yml` (Function App deploy).
- Frontend secret: `AZURE_STATIC_WEB_APPS_API_TOKEN` (from SWA).
- Backend uses OIDC via `azure/login`; required secrets: `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, plus set `FUNCTION_APP_NAME` in the workflow (or pass as a secret).
- Backend build targets Linux (GOOS=linux, GOARCH=amd64) to match the Function App runtime.
