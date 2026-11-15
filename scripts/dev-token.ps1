# Prints a development bearer token for the API (ADR-7). It wraps `dotnet user-jwts`, which keeps the signing key
# in your user secrets, so nothing secret is in the repository and the API never issues tokens itself.
# Usage:  pwsh scripts/dev-token.ps1 [-Merchant merchant-1] [-Scopes payments:create,payments:read]
# Then:   curl -H "Authorization: Bearer <token>" https://localhost:7092/api/payments/<id>
param(
    [string]$Merchant = 'merchant-1',
    [string[]]$Scopes = @('payments:create', 'payments:read'),
    [string]$Name = 'dev-user'
)

$project = Join-Path $PSScriptRoot '..\src\PaymentGateway.Api'
$scopeArgs = $Scopes | ForEach-Object { '--scope'; $_ }

dotnet user-jwts create --project $project --name $Name @scopeArgs --claim "merchant_id=$Merchant" --output token
