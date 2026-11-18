param([string]$SdkPath)
$ErrorActionPreference='Stop'
if (!$SdkPath) { throw 'Pass -SdkPath pointing to extracted Microsoft.Web.WebView2 1.0.4258.31 NuGet package.' }
$root=Split-Path $PSScriptRoot -Parent
$dest=Join-Path $root 'Build\Windows\Browser'
New-Item -ItemType Directory -Force $dest | Out-Null
Copy-Item (Join-Path $SdkPath 'lib\net462\Microsoft.Web.WebView2.Core.dll'),(Join-Path $SdkPath 'lib\net462\Microsoft.Web.WebView2.WinForms.dll'),(Join-Path $SdkPath 'runtimes\win-x64\native\WebView2Loader.dll'),(Join-Path $SdkPath 'LICENSE.txt'),(Join-Path $SdkPath 'NOTICE.txt') $dest
$rsp=Join-Path $env:TEMP ('clarenza-browser-'+[guid]::NewGuid()+'.rsp')
try {
@"
/nologo
/target:winexe
/platform:x64
/out:"$dest\ClarenzaBrowser.exe"
/r:System.Windows.Forms.dll
/r:System.Drawing.dll
/r:"$dest\Microsoft.Web.WebView2.Core.dll"
/r:"$dest\Microsoft.Web.WebView2.WinForms.dll"
"$PSScriptRoot\Browser.cs"
"@ | Set-Content $rsp
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" "@$rsp"
if($LASTEXITCODE -ne 0){throw 'Browser compilation failed'}
} finally { Remove-Item -LiteralPath $rsp -ErrorAction SilentlyContinue }
