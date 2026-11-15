# One command to take the API for a spin: starts the bank simulator (if Docker is there), makes a development token,
# copies it to your clipboard and runs the API. Press Ctrl+C to stop it; nothing is left running.
# Usage:  pwsh scripts/start.ps1                    # simulator + token + API, then open the docs in the browser
#         pwsh scripts/start.ps1 -TokenOnly         # just make a token, copy it and print a curl example
#         pwsh scripts/start.ps1 -Merchant merchant-2 -Scopes payments:read
param(
    [string]$Merchant = 'merchant-1',
    [string[]]$Scopes = @('payments:create', 'payments:read'),
    [int]$Port = 7092,
    [switch]$TokenOnly,
    [switch]$NoBrowser,
    [switch]$Pause   # keep the window open at the end (used by the Visual Studio "Get dev token" launch profile)
)

$ErrorActionPreference = 'Stop'
$root = Join-Path $PSScriptRoot '..'
$project = Join-Path $root 'src\PaymentGateway.Api'
$https = $Port -eq 7092
$baseUrl = if ($https) { "https://localhost:$Port" } else { "http://localhost:$Port" }

function Start-BankSimulator {
    if (Test-NetConnection localhost -Port 8080 -InformationLevel Quiet -WarningAction SilentlyContinue) { 'Bank simulator: already running on :8080'; return }
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { Write-Warning 'Docker not found: valid payments will answer 502 (no bank). Everything else works.'; return }
    Push-Location $root
    try { docker-compose up -d | Out-Null; 'Bank simulator: started on :8080' } catch { Write-Warning "Could not start the simulator: $_" } finally { Pop-Location }
}

function New-DevToken {
    $token = (& (Join-Path $PSScriptRoot 'dev-token.ps1') -Merchant $Merchant -Scopes $Scopes | Select-Object -Last 1)
    try { Set-Clipboard -Value $token; $copied = ' (copied to your clipboard)' } catch { $copied = '' }
    "Token for $Merchant [$($Scopes -join ' ')]$copied"
    $token
}

try {
    if (-not $TokenOnly) { Start-BankSimulator }
    $lines = @(New-DevToken)
    $lines[0]
    $token = $lines[-1]

    $example = "curl -k -X POST $baseUrl/api/payments -H `"Authorization: Bearer <token>`" -H `"Content-Type: application/json`" " +
               "-d '{`"cardNumber`":`"2222405343248877`",`"expiryMonth`":4,`"expiryYear`":2030,`"currency`":`"GBP`",`"amount`":100,`"cvv`":`"123`"}'"
    ''
    'Try it (a card ending in an odd digit is Authorized, even is Declined, 0 makes the bank fail):'
    "  $example"
    "  Scalar UI: $baseUrl/scalar/v1  (click Authorize and paste the token)"
    ''

    if ($TokenOnly) {
        'YOUR TOKEN is below, and is already on your clipboard (press Ctrl+V to paste it). It lasts about 3 months.'
        'This only made a token; it did not start the API. Next:'
        '  1. Start the API: in Visual Studio pick the PaymentGateway.Api profile and press F5, or run: dotnet run --project src/PaymentGateway.Api'
        '  2. In the Scalar page that opens, click Authorize and paste the token (or send it as: Authorization: Bearer <token>).'
        '  3. Send a POST /api/payments (see the curl example above). A 401 later means the token has expired: run this again.'
        ''
        $token
        return
    }

    'Starting the API. Your token is on your clipboard: when the Scalar page opens, click Authorize and press Ctrl+V. Ctrl+C here stops the API.'
    if (-not $NoBrowser) { Start-Job { Start-Sleep 6; Start-Process "$using:baseUrl/scalar/v1" } | Out-Null }
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    if ($https) { dotnet run --project $project }
    else { $env:ASPNETCORE_URLS = $baseUrl; dotnet run --no-launch-profile --project $project }
}
finally {
    if ($Pause) { Read-Host 'Press Enter to close' | Out-Null }
}
