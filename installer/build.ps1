# Builds the installer: publishes the app, then packages it with Inno Setup.
# Needs the .NET 10 SDK and Inno Setup 6 (https://jrsoftware.org/isinfo.php).
param([string]$Version = "1.0.0")

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$publish = Join-Path $root "publish"

if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }

# One exe that needs the .NET Desktop Runtime installed (the installer checks for it).
dotnet publish (Join-Path $root "src\RazerHelper\RazerHelper.csproj") `
    -c Release -r win-x64 --self-contained false `
    -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false `
    -o $publish
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

$iscc = @(
    "$env:ProgramFiles(x86)\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw "Inno Setup 6 was not found. Install it from https://jrsoftware.org/isinfo.php" }

& $iscc "/DAppVersion=$Version" (Join-Path $PSScriptRoot "RazerHelper.iss")
if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed" }

Write-Host "Installer: $(Join-Path $root "dist\RazerHelper-Setup-$Version.exe")"
