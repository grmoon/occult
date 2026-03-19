<#
.SYNOPSIS
    Deploys the OccultFrontend to Azure Static Web Apps.

.DESCRIPTION
    Builds the Vite project and deploys the output to the
    occult-web-app Azure Static Web App using the SWA CLI.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ProjectDir = Split-Path -Parent $PSScriptRoot
$AppName    = 'occult-web-app'

Push-Location $ProjectDir
try {
    Write-Host 'Installing dependencies...' -ForegroundColor Cyan
    npm ci

    Write-Host 'Building project...' -ForegroundColor Cyan
    $env:VITE_API_HOST = 'https://occult-function-app-e9effga5bfgae5f6.westus-01.azurewebsites.net'
    npm run build
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed with exit code $LASTEXITCODE"
    }

    Write-Host "Deploying to $AppName..." -ForegroundColor Cyan
    npx @azure/static-web-apps-cli deploy ./dist `
        --app-name $AppName `
        --no-use-keychain `
        --env production
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Deployment failed with exit code $LASTEXITCODE"
    }

    Write-Host 'Deployment complete.' -ForegroundColor Green
}
finally {
    Pop-Location
}