<#
.SYNOPSIS
    Runs Quantum Wake as if it had just been installed, without touching the
    real one.

.DESCRIPTION
    Everything the app owns - session cache, community and UEX downloads, jobs,
    overlay layout, the "setup done" marker - lives under one folder. This
    starts a copy pointed at a scratch folder instead, on a different port, so
    the first-flight wizard runs, nothing is enabled, and no history exists.

    Your real data is never opened. Delete the scratch folder (or pass -Reset)
    to start from nothing again.

.PARAMETER DataPath
    Where the pretend install keeps its data. Defaults to a folder in TEMP.

.PARAMETER Port
    Port for the pretend install. Defaults to 31347, leaving the real one on
    31337 alone.

.PARAMETER Reset
    Empties the scratch folder first, so the run is a true first run.

.PARAMETER Overlay
    Launches the full QuantumWake.exe (tray and overlay) instead of just the
    dashboard server.

.EXAMPLE
    .\fresh-run.ps1 -Reset
    A brand new install on http://127.0.0.1:31347.
#>
[CmdletBinding()]
param(
    [string]$DataPath = (Join-Path $env:TEMP 'QuantumWake-FreshRun'),
    [int]$Port = 31347,
    [switch]$Reset,
    [switch]$Overlay
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

# Refuse to point at the real data folder, which this script exists to protect.
$real = Join-Path $env:LOCALAPPDATA 'Quantumwake'
if ([System.IO.Path]::GetFullPath($DataPath) -eq [System.IO.Path]::GetFullPath($real)) {
    throw "That is the real data folder. Pick another -DataPath."
}

if ($Reset -and (Test-Path $DataPath)) {
    Write-Host "Emptying $DataPath" -ForegroundColor Yellow
    Remove-Item $DataPath -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $DataPath | Out-Null

Write-Host ""
Write-Host "Quantum Wake - pretend first run" -ForegroundColor Cyan
Write-Host "  data  : $DataPath"
Write-Host "  port  : $Port"
Write-Host "  real data at $real is not touched."
Write-Host ""

$project = if ($Overlay) { 'src\Quantumwake.Overlay' } else { 'src\Quantumwake.Server' }

Write-Host "Building..." -ForegroundColor DarkGray
dotnet build (Join-Path $root $project) -c Debug --nologo -v q

if ($LASTEXITCODE -ne 0) { throw "Build failed." }

if ($Overlay) {
    # The whole app: tray icon, overlay window and dashboard together.
    $exe = Get-ChildItem (Join-Path $root "$project\bin\Debug") -Recurse -Filter 'QuantumWake.exe' |
        Select-Object -First 1 -ExpandProperty FullName

    Write-Host "Starting QuantumWake.exe - look for the tray icon." -ForegroundColor Green
    Start-Process $exe -ArgumentList @('--data', $DataPath, '--Port', $Port)
    Start-Sleep -Seconds 4
} else {
    Write-Host "Starting the dashboard. Ctrl+C stops it." -ForegroundColor Green
    Start-Process "http://127.0.0.1:$Port/"
    dotnet run --project (Join-Path $root $project) --no-build -c Debug -- --data $DataPath --Port $Port
}
