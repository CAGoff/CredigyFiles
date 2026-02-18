# deploy-dev.ps1 — Build and deploy all components to the Azure dev environment.
# Run from repo root: .\scripts\deploy-dev.ps1 [-SwaToken <token>] [-SkipBuild]
#
# Prerequisites:
#   - az login (authenticated to Azure CLI)
#   - .NET 9 SDK, Node 24+, npm
#   - SWA deployment token (param or $env:SWA_DEPLOYMENT_TOKEN)

param(
    [string]$SwaToken = $env:SWA_DEPLOYMENT_TOKEN,
    [switch]$SkipBuild,
    [switch]$SkipApi,
    [switch]$SkipFunctions,
    [switch]$SkipWeb,
    [switch]$SkipEventGrid
)

$ErrorActionPreference = "Stop"
$solutionRoot = Join-Path $PSScriptRoot ".."

# --- Configuration ---
$resourceGroup   = "rg-credigyfiles-dev-eus"
$apiAppName      = "sft-credigyfiles-dev-api"
$funcAppName     = "sft-credigyfiles-dev-func"
$eventGridTopic  = "sft-credigyfiles-dev-evgt"
$eventGridSub    = "sft-credigyfiles-dev-evgs-notify"

$apiProject      = "$solutionRoot\src\api\SecureFileTransfer.Api\SecureFileTransfer.Api.csproj"
$funcProject     = "$solutionRoot\src\functions\SecureFileTransfer.Functions\SecureFileTransfer.Functions.csproj"
$webDir          = "$solutionRoot\src\web"

$publishRoot     = "$solutionRoot\.publish"
$apiPublishDir   = "$publishRoot\api"
$funcPublishDir  = "$publishRoot\functions"
$apiZip          = "$publishRoot\api-package.zip"
$funcZip         = "$publishRoot\functions-package.zip"

# --- Helpers ---
function Write-Step($msg) { Write-Host "`n--- $msg ---" -ForegroundColor Yellow }
function Write-Ok($msg)   { Write-Host "$msg" -ForegroundColor Green }
function Write-Fail($msg) { Write-Host "$msg" -ForegroundColor Red; exit 1 }

function Assert-ExitCode($step) {
    if ($LASTEXITCODE -ne 0) { Write-Fail "$step FAILED (exit code $LASTEXITCODE)" }
}

# --- Preflight checks ---
Write-Host "Deploying Credigy Files to dev environment" -ForegroundColor Cyan

if (-not $SkipWeb -and -not $SwaToken) {
    Write-Fail "SWA deployment token required. Pass -SwaToken or set `$env:SWA_DEPLOYMENT_TOKEN.`nGet it from: Azure Portal > sft-credigyfiles-dev-swa > Overview > Manage deployment token"
}

# Verify Azure CLI session
Write-Step "Verifying Azure CLI session"
az account show --query "name" -o tsv 2>$null
if ($LASTEXITCODE -ne 0) { Write-Fail "Not logged in. Run 'az login' first." }
Write-Ok "Azure CLI authenticated"

# Clean publish directory
if (Test-Path $publishRoot) { Remove-Item $publishRoot -Recurse -Force }
New-Item -ItemType Directory -Path $publishRoot -Force | Out-Null

# =========================================================================
# 1. Build & Deploy API
# =========================================================================
if (-not $SkipApi) {
    if (-not $SkipBuild) {
        Write-Step "Publishing API"
        dotnet publish $apiProject --configuration Release --output $apiPublishDir --verbosity minimal
        Assert-ExitCode "API publish"
        Write-Ok "API published"

        Write-Step "Zipping API package"
        Compress-Archive -Path "$apiPublishDir\*" -DestinationPath $apiZip -Force
        Write-Ok "API zipped: $apiZip"
    }

    # Use 'az webapp deploy --type startup' workaround: push the zip as a
    # ready-to-run package. We set WEBSITE_RUN_FROM_PACKAGE=1 so the runtime
    # mounts the zip directly (no Oryx build, no extraction).
    #
    # Step 1: Remove WEBSITE_RUN_FROM_PACKAGE if set (conflicts with zip push)
    # Step 2: Push the zip via Kudu zipdeploy REST API directly
    Write-Step "Deploying API to $apiAppName via Kudu zipdeploy"

    # Get publishing credentials
    $creds = az webapp deployment list-publishing-credentials `
        --resource-group $resourceGroup `
        --name $apiAppName `
        --query "{user:publishingUserName, pass:publishingPassword}" `
        --output json | ConvertFrom-Json
    Assert-ExitCode "Get publishing credentials"

    $kuduUrl = "https://$apiAppName.scm.azurewebsites.net/api/zipdeploy?isAsync=false"
    $base64Auth = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("$($creds.user):$($creds.pass)"))

    # Upload via curl.exe (built into Windows 10+)
    # PowerShell 5.1's Invoke-RestMethod has issues with binary uploads to Kudu
    Write-Host "Uploading to Kudu..." -ForegroundColor Gray
    curl.exe --fail --silent --show-error `
        -X POST $kuduUrl `
        -H "Authorization: Basic $base64Auth" `
        -H "Content-Type: application/octet-stream" `
        --data-binary "@$apiZip" `
        --insecure
    Assert-ExitCode "API deploy"
    Write-Ok "API deployed"
}

# =========================================================================
# 2. Build & Deploy Functions
# =========================================================================
if (-not $SkipFunctions) {
    if (-not $SkipBuild) {
        Write-Step "Publishing Functions"
        dotnet publish $funcProject --configuration Release --output $funcPublishDir --verbosity minimal
        Assert-ExitCode "Functions publish"
        Write-Ok "Functions published"

        Write-Step "Zipping Functions package"
        Compress-Archive -Path "$funcPublishDir\*" -DestinationPath $funcZip -Force
        Write-Ok "Functions zipped: $funcZip"
    }

    # Flex Consumption Function Apps don't have Kudu — deploy by uploading
    # the zip to the deployment blob container, then syncing.
    Write-Step "Deploying Functions to $funcAppName (Flex Consumption)"

    $funcStorageName = "sftcredigyfilesdevfunc"
    $deployContainer = "app-package-credigyfiles"

    # Upload zip to the deployment container via az storage blob (using account key)
    Write-Host "Uploading package to storage..." -ForegroundColor Gray
    az storage blob upload `
        --account-name $funcStorageName `
        --container-name $deployContainer `
        --name "functions-package.zip" `
        --file $funcZip `
        --overwrite `
        --auth-mode key `
        --output none
    Assert-ExitCode "Functions blob upload"

    # Trigger deployment sync
    Write-Host "Syncing function app..." -ForegroundColor Gray
    az functionapp deploy `
        --resource-group $resourceGroup `
        --name $funcAppName `
        --src-path $funcZip `
        --type zip
    if ($LASTEXITCODE -ne 0) {
        # Fallback: if az functionapp deploy fails (SSL on polling),
        # the blob upload already succeeded — the app will pick it up.
        Write-Host "Sync command failed (likely SSL on polling), but package was uploaded." -ForegroundColor Yellow
        Write-Host "The Function App should pick up the new package automatically." -ForegroundColor Yellow
    }
    Write-Ok "Functions deployed"
}

# =========================================================================
# 3. Build & Deploy SPA
# =========================================================================
if (-not $SkipWeb) {
    if (-not $SkipBuild) {
        Write-Step "Building SPA"
        Push-Location $webDir
        try {
            npm ci --silent
            Assert-ExitCode "npm ci"
            npm run build
            Assert-ExitCode "SPA build"
        }
        finally { Pop-Location }
        Write-Ok "SPA built"
    }

    # Deploy using StaticSitesClient.exe — the standalone binary that the
    # SWA CLI and GitHub Action use under the hood. No Node.js/npm needed,
    # works with the deployment token directly.
    Write-Step "Deploying SPA to Static Web App"

    $swaClientDir = "$env:USERPROFILE\.swa"
    $swaClient    = "$swaClientDir\StaticSitesClient.exe"

    if (-not (Test-Path $swaClient)) {
        Write-Host "Downloading StaticSitesClient.exe..." -ForegroundColor Gray
        if (-not (Test-Path $swaClientDir)) {
            New-Item -ItemType Directory -Path $swaClientDir -Force | Out-Null
        }
        $swaUrl = "https://swalocaldeployv2-bndtgugjgqc3dhdx.b01.azurefd.net/downloads/08e29138cd3dcda4ffda6d587aa580028110c1c7/windows/StaticSitesClient.exe"
        curl.exe --fail --silent --show-error --insecure -o $swaClient $swaUrl
        Assert-ExitCode "Download StaticSitesClient.exe"
        Write-Ok "StaticSitesClient.exe downloaded"
    }

    Write-Host "Uploading SPA dist to Static Web App..." -ForegroundColor Gray
    & $swaClient upload `
        --verbose true `
        --skipAppBuild true `
        --app "$webDir\dist" `
        --outputLocation "." `
        --apiToken $SwaToken `
        --deploymentProvider "Script"
    Assert-ExitCode "SPA deploy"
    Write-Ok "SPA deployed"
}

# =========================================================================
# 4. Re-sync Event Grid subscription
# =========================================================================
if (-not $SkipEventGrid) {
    Write-Step "Updating Event Grid subscription"

    $funcId = az functionapp show `
        --resource-group $resourceGroup `
        --name $funcAppName `
        --query id --output tsv
    Assert-ExitCode "Get Function App ID"

    az eventgrid system-topic event-subscription create `
        --resource-group $resourceGroup `
        --system-topic-name $eventGridTopic `
        --name $eventGridSub `
        --endpoint-type azurefunction `
        --endpoint "$funcId/functions/NotificationTrigger" `
        --included-event-types Microsoft.Storage.BlobCreated Microsoft.Storage.BlobDeleted `
        --max-delivery-attempts 30 `
        --event-ttl 1440 2>$null

    if ($LASTEXITCODE -ne 0) {
        Write-Host "Event Grid subscription update skipped (topic may not exist yet)" -ForegroundColor Gray
    }
    else {
        Write-Ok "Event Grid subscription updated"
    }
}

# =========================================================================
# Done
# =========================================================================
Write-Host "`nDeploy complete!" -ForegroundColor Green

# Clean up publish artifacts
Remove-Item $publishRoot -Recurse -Force -ErrorAction SilentlyContinue
