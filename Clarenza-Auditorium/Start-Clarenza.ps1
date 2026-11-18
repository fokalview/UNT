param([switch]$EnableAI)
$ErrorActionPreference = 'Stop'
$projectDir = $PSScriptRoot
$nodeCommand = Get-Command node -ErrorAction SilentlyContinue
$nodePath = if ($nodeCommand) { $nodeCommand.Source } else { Join-Path $env:USERPROFILE '.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe' }
if (-not (Test-Path -LiteralPath $nodePath)) { throw 'Install Node.js 22 or newer, then run this launcher again.' }
if ($EnableAI) {
    $env:OPENAI_MODEL = Read-Host 'OpenAI model ID with Structured Outputs support'
    $secret = Read-Host 'OpenAI API key (hidden; used only for this session)' -AsSecureString
    $secretPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secret)
    try { $env:OPENAI_API_KEY = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($secretPointer) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($secretPointer) }
}
$existing = $false
try { $health = Invoke-RestMethod -Method Post -Uri 'http://127.0.0.1:47831/health' -ContentType 'application/json' -Body '{}' -TimeoutSec 2; $existing = $health.ready } catch {}
if ($existing -and ($health.serviceVersion -ne 4 -or !$health.accountsReady)) { throw "An older Clarenza service is running on port 47831. Close the previous Clarenza launcher/session and relaunch this version. This service cannot sign you in." }
if ($existing -and $EnableAI) { throw 'A Clarenza service is already running. Close the previous Clarenza launcher/session before enabling AI.' }
$serviceProcess = $null
try {
    if (-not $existing) {
        $serviceProcess = Start-Process -FilePath $nodePath -ArgumentList ('"' + (Join-Path $projectDir 'Service\server.js') + '"') -WindowStyle Hidden -PassThru
        $ready = $false
        for ($attempt = 0; $attempt -lt 20; $attempt++) {
            Start-Sleep -Milliseconds 250
            try { $ready = (Invoke-RestMethod -Method Post -Uri 'http://127.0.0.1:47831/health' -ContentType 'application/json' -Body '{}' -TimeoutSec 1).ready; if ($ready) { break } } catch {}
        }
        if (-not $ready) { throw 'The local narration/AI service did not start.' }
    }
    # The service already inherited its key; keep it out of the Unity process.
    Remove-Item Env:OPENAI_API_KEY -ErrorAction SilentlyContinue
    Start-Process -FilePath (Join-Path $projectDir 'Build\Windows\Clarenza.exe') -Wait
} finally {
    if ($null -ne $serviceProcess -and -not $serviceProcess.HasExited) { Stop-Process -Id $serviceProcess.Id -ErrorAction SilentlyContinue }
    if ($EnableAI) { Remove-Item Env:OPENAI_API_KEY -ErrorAction SilentlyContinue }
}

