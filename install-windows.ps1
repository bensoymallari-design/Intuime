# Builds Intuime for Windows and an installer you can double-click.
# Usage, from this directory:  powershell -ExecutionPolicy Bypass -File .\install-windows.ps1

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "Installing the .NET 8 SDK..."
    winget install --id Microsoft.DotNet.SDK.8 -e --accept-package-agreements --accept-source-agreements
}

Write-Host "Publishing Intuime..."
dotnet publish src/Intuime/Intuime.csproj -c Release -r win-x64 --self-contained true -p:DebugType=None -p:DebugSymbols=false -o publish/win-x64
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

function Find-InnoCompiler {
    $candidates = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    )
    foreach ($path in $candidates) {
        if (Test-Path $path) { return $path }
    }
    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }
    return $null
}

$iscc = Find-InnoCompiler
if (-not $iscc) {
    Write-Host "Installing Inno Setup so the installer can be built..."
    winget install --id JRSoftware.InnoSetup -e --accept-package-agreements --accept-source-agreements
    $iscc = Find-InnoCompiler
}

if (-not $iscc) {
    Write-Host ""
    Write-Host "The app was published, but Inno Setup was not found, so the installer was not created."
    Write-Host "  App:        publish\win-x64\Intuime.exe"
    Write-Host "Install Inno Setup 6, then run this script again to create publish\Intuime-Setup.exe."
    exit 1
}

Write-Host "Creating the installer..."
& $iscc "installer\Intuime.iss"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host ""
Write-Host "Intuime is ready:"
Write-Host "  Installer:  publish\Intuime-Setup.exe"
Write-Host "  App only:   publish\win-x64\Intuime.exe"
Write-Host "Double-click the installer. It puts Intuime on the Start menu and the desktop, with the app logo."
