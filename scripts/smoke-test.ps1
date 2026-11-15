# Starts the API, runs the given checks against it and ALWAYS stops it again, so no process is left
# holding the build output open (a leftover dotnet process locks the DLL and breaks the next build).
# Usage: pwsh scripts/smoke-test.ps1 [-Port 5199]
param([int]$Port = 5199)

$project = Join-Path $PSScriptRoot '..\src\PaymentGateway.Api'
$process = $null

try {
    $env:ASPNETCORE_URLS = "http://localhost:$Port"
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    # Run from the project directory so appsettings.json is found (startup validation fails fast otherwise).
    $process = Start-Process dotnet -ArgumentList 'run', '--no-launch-profile', '--project', $project `
        -WorkingDirectory $project -PassThru -WindowStyle Hidden

    $ready = $false
    for ($i = 0; $i -lt 30 -and -not $ready; $i++) {
        Start-Sleep -Seconds 1
        try { Invoke-WebRequest "http://localhost:$Port/scalar/v1" -UseBasicParsing | Out-Null; $ready = $true } catch { }
    }
    if (-not $ready) { throw 'The API did not start.' }

    $token = & (Join-Path $PSScriptRoot 'dev-token.ps1')
    $headers = @{ Authorization = "Bearer $token" }
    $body = '{"cardNumber":"2222405343248877","expiryMonth":4,"expiryYear":2030,"currency":"GBP","amount":100,"cvv":"123"}'
    foreach ($check in @(
        @{ Name = 'POST without a token (-> 401)';   Method = 'POST'; Path = '/api/payments'; Body = $body; Anonymous = $true },
        @{ Name = 'POST (bank unreachable -> 502)'; Method = 'POST'; Path = '/api/payments'; Body = $body },
        @{ Name = 'POST with a body over the size limit (-> 413)'; Method = 'POST'; Path = '/api/payments'; Body = ('x' * 10000) },
        @{ Name = 'POST malformed (-> 400)';        Method = 'POST'; Path = '/api/payments'; Body = '{ nope' },
        @{ Name = 'GET unknown id (-> 404)';        Method = 'GET';  Path = "/api/payments/$([guid]::NewGuid())"; Body = $null })) {
        $params = @{ Uri = "http://localhost:$Port$($check.Path)"; Method = $check.Method; ContentType = 'application/json'
                     UseBasicParsing = $true; SkipHttpErrorCheck = $true }
        if ($check.Body) { $params.Body = $check.Body }
        if (-not $check.Anonymous) { $params.Headers = $headers.Clone() }
        $response = Invoke-WebRequest @params
        "{0}: {1}" -f $check.Name, [int]$response.StatusCode
    }
}
finally {
    if ($process) {
        # /T also ends the child process that `dotnet run` starts.
        taskkill /PID $process.Id /T /F | Out-Null
    }
}
