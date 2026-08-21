<#
.SYNOPSIS
    Builds and launches Quantumwake.

.DESCRIPTION
    Starts the dashboard on http://127.0.0.1:31337 and, unless -NoOverlay is
    given, the transparent in-game overlay as well.

    The overlay is only visible when Star Citizen runs in Borderless Windowed.
    An always-on-top window is not composited over exclusive fullscreen - that
    is the cost of staying clear of Easy Anti-Cheat, which this tool does by
    never injecting code or hooking the game.

.PARAMETER Path
    Star Citizen channel directory. Auto-detected when omitted.

.PARAMETER Lan
    Bind to all interfaces so a tablet or phone can act as the second screen.
    Off by default: standalone mode is loopback only.

.PARAMETER NoOverlay
    Dashboard only.

.PARAMETER Rescan
    Force a full re-parse, ignoring the cache.

.EXAMPLE
    .\start.ps1
    .\start.ps1 -NoOverlay -Lan
#>
[CmdletBinding()]
param(
    [string]$Path,
    [switch]$Lan,
    [switch]$NoOverlay,
    [switch]$Rescan,
    [int]$Port = 31337
)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

Write-Host 'Building…' -ForegroundColor Cyan
dotnet build Quantumwake.slnx -c Release -v q --nologo | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

if ($Rescan) {
    $db = Join-Path $env:LOCALAPPDATA 'Quantumwake\sessions.db'
    if (Test-Path $db) {
        Remove-Item $db -Force
        Write-Host 'Cache cleared; the next scan will re-read every log.' -ForegroundColor Yellow
    }
}

$serverArgs = @('--project', 'src\Quantumwake.Server', '-c', 'Release', '--no-build')
if ($Path) { $serverArgs += @('--path', $Path) }
if ($Lan)  { $serverArgs += @('--Lan', 'true') }
if ($Port -ne 31337) { $serverArgs += @('--Port', "$Port") }

Write-Host "Starting server on http://127.0.0.1:$Port …" -ForegroundColor Cyan
$server = Start-Process dotnet -ArgumentList $serverArgs -PassThru -WindowStyle Hidden

# Wait for the first response rather than sleeping a fixed amount.
$ready = $false
foreach ($attempt in 1..60) {
    Start-Sleep -Milliseconds 500
    try {
        Invoke-WebRequest "http://127.0.0.1:$Port/api/install" -UseBasicParsing -TimeoutSec 2 | Out-Null
        $ready = $true
        break
    } catch { }
}

if (-not $ready) {
    Write-Host 'The server did not come up. Run it directly to see why:' -ForegroundColor Red
    Write-Host '  dotnet run --project src\Quantumwake.Server -c Release' -ForegroundColor DarkGray
    if (-not $server.HasExited) { Stop-Process $server.Id -Force }
    exit 1
}

Write-Host "Dashboard ready: http://127.0.0.1:$Port" -ForegroundColor Green
Start-Process "http://127.0.0.1:$Port"

if (-not $NoOverlay) {
    $overlay = 'src\Quantumwake.Overlay\bin\Release\net10.0-windows\Quantumwake.Overlay.exe'
    if (Test-Path $overlay) {
        Start-Process $overlay
        Write-Host 'Overlay running. Ctrl+Alt+O toggles click-through.' -ForegroundColor Green
        Write-Host 'Run Star Citizen in Borderless Windowed for it to be visible.' -ForegroundColor DarkGray
    } else {
        Write-Host "Overlay not found at $overlay" -ForegroundColor Yellow
    }
}

Write-Host ''
Write-Host 'Press Ctrl+C to stop the server.' -ForegroundColor DarkGray

try {
    Wait-Process -Id $server.Id
} finally {
    if (-not $server.HasExited) { Stop-Process $server.Id -Force }
}
