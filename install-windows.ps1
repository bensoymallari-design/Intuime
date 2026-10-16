# Builds a self-contained Intuime folder you can run on Windows.
# Usage, from this directory:  powershell -ExecutionPolicy Bypass -File .\install-windows.ps1

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "Installing the .NET 8 SDK..."
    winget install --id Microsoft.DotNet.SDK.8 -e --accept-package-agreements --accept-source-agreements
}

dotnet publish src/Intuime/Intuime.csproj -c Release -r win-x64 --self-contained true -o publish/win-x64
Write-Host ""
Write-Host "Intuime is ready:"
Write-Host "  publish\win-x64\Intuime.exe"
Write-Host "Double-click that file. The gallery sample opens in the composer. Press Play to run it."
