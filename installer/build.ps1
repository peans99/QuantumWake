# Builds the Windows installer around an already-published QuantumWake.exe.
#
#   ./installer/build.ps1 -Exe dist/app/QuantumWake.exe -Version 0.22.3 -Out dist/installer
#
# The release workflow runs exactly this after publishing, so the installer is
# built from the same bytes as the bare exe and Defender's scan of dist covers
# both. WiX comes from the repository's tool manifest (dotnet-tools.json),
# pinned to 5.0.2: WiX 6 moved to a licence with a maintenance fee, and 5 does
# everything this needs.
param(
    [Parameter(Mandatory)] [string] $Exe,
    [Parameter(Mandatory)] [string] $Version,
    [Parameter(Mandatory)] [string] $Out
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

# Windows Installer keeps three fields, each a number: 0.22.3 is fine, a tag
# suffix is not, and a fourth field is silently ignored by upgrades.
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "Installer version must be major.minor.patch, got '$Version'." }

$exePath = (Resolve-Path $Exe).Path
New-Item -ItemType Directory -Force $Out | Out-Null
$msi = Join-Path (Resolve-Path $Out).Path "QuantumWake-$Version-win-x64.msi"

Push-Location $root
try {
    dotnet tool restore | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'dotnet tool restore failed' }

    dotnet wix build installer/QuantumWake.wxs -arch x64 `
        -d "Version=$Version" -d "Exe=$exePath" -d "Icon=$(Join-Path $root 'src/Quantumwake.Overlay/app.ico')" `
        -o $msi
    if ($LASTEXITCODE -ne 0) { throw 'wix build failed' }

    # The internal consistency evaluators, the checks the Windows SDK's Orca
    # runs. A per-user package that breaks one of them installs on the machine
    # that built it and fails on the next.
    dotnet wix msi validate $msi
    if ($LASTEXITCODE -ne 0) { throw 'the installer failed validation' }
}
finally {
    # wix leaves its symbol file beside the package; only the .msi ships.
    Remove-Item ([IO.Path]::ChangeExtension($msi, '.wixpdb')) -ErrorAction SilentlyContinue
    Pop-Location
}

Write-Host "Built $msi ($([math]::Round((Get-Item $msi).Length / 1MB, 1)) MB)"
