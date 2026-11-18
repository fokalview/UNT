$ErrorActionPreference='Stop'
$node=(Get-Command node -ErrorAction SilentlyContinue).Source
if(!$node){$node=Join-Path $env:USERPROFILE '.cache\codex-runtimes\codex-primary-runtime\dependencies\node\bin\node.exe'}
$username=Read-Host 'Account username'
$role=Read-Host 'Role (admin, teacher, student)'
$secret=Read-Host 'Password (at least 12 characters)' -AsSecureString
$ptr=[Runtime.InteropServices.Marshal]::SecureStringToBSTR($secret)
try {
 $payload=@{username=$username;role=$role;password=[Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr)} | ConvertTo-Json -Compress
 $payload | & $node (Join-Path $PSScriptRoot 'Service\accounts.js')
 if($LASTEXITCODE -ne 0){throw 'Account creation failed.'}
}finally{[Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr);$payload=$null}
