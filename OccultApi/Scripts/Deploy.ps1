<#
.SYNOPSIS
    Deploys the OccultApi Azure Function App.

.DESCRIPTION
    Publishes the OccultApi project to the occult-function-app
    Azure Function App using Azure Functions Core Tools.
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ProjectDir    = Split-Path -Parent $PSScriptRoot
$FunctionApp   = 'occult-function-app'
$ResourceGroup = 'occult-rg'
$CorsOrigin    = 'https://victorious-moss-04783031e.1.azurestaticapps.net'

Push-Location $ProjectDir
try {
    Write-Host "Deploying to $FunctionApp..." -ForegroundColor Cyan
    func azure functionapp publish $FunctionApp
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Deployment failed with exit code $LASTEXITCODE"
    }

    # Ensure app settings are configured
    Write-Host 'Configuring app settings...' -ForegroundColor Cyan
    az functionapp config appsettings set `
        --name $FunctionApp `
        --resource-group $ResourceGroup `
        --settings `
            AiDeploymentName="gpt-4.1" `
            AiServicesEndpoint="https://occult-foundry.services.ai.azure.com/" `
            ManagedIdentityClientId="7799f367-dc92-434c-8cb6-3ad1553a66bf" `
            AiSpeechRegion="westus" `
        --output none
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to configure app settings"
    }

    # Ensure CORS is configured
    $cors = az functionapp cors show --name $FunctionApp --resource-group $ResourceGroup | ConvertFrom-Json
    if ($cors.allowedOrigins -notcontains $CorsOrigin) {
        Write-Host "Adding CORS origin: $CorsOrigin" -ForegroundColor Cyan
        az functionapp cors add --name $FunctionApp --resource-group $ResourceGroup --allowed-origins $CorsOrigin
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to add CORS origin"
        }
    } else {
        Write-Host "CORS origin already configured." -ForegroundColor DarkGray
    }

    Write-Host 'Deployment complete.' -ForegroundColor Green
}
finally {
    Pop-Location
}
