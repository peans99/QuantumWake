# Builds the Microsoft Store package around an already-published QuantumWake.exe.
#
#   ./installer/build-msix.ps1 -Exe dist/app/QuantumWake.exe -Version 1.0.1 -Out dist/store [-Release]
#
# The package is unsigned on purpose: the Store signs what it accepts, which is
# why the Store ships as MSIX rather than as the MSI, which would need a
# certificate of our own. An unsigned package installs nowhere else, so it is a
# workflow artifact for uploading to Partner Center, never a release download.
# docs/store.md has the rest.
#
# MakeAppx comes from Microsoft's own Microsoft.Windows.SDK.BuildTools NuGet
# package, pinned by version and SHA-512, so neither this machine nor the
# runner needs the Windows SDK installed.
param(
    [Parameter(Mandatory)] [string] $Exe,
    [Parameter(Mandatory)] [string] $Version,
    [Parameter(Mandatory)] [string] $Out,

    # Refuses the placeholder identity. CI packs around a stand-in exe and the
    # placeholder to prove the manifest still packs; a release must not.
    [switch] $Release
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

$buildToolsVersion = '10.0.28000.2705'
$buildToolsSha512 = 'e3b929e3678c6fcac5ddb8c3b991d59331916fd16bef0c85643213dac5abdd2b709d30a80c7222b037b448f586c1eaf0554c3f79040efcec436f46181db7cdbc'

# MSIX versions have four fields and the Store requires the last to be 0.
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw "Package version must be major.minor.patch, got '$Version'." }
$packageVersion = "$Version.0"

$identity = Get-Content (Join-Path $PSScriptRoot 'msix/store-identity.json') -Raw | ConvertFrom-Json
if ($Release -and ($identity.IdentityName -like 'PLACEHOLDER*' -or $identity.Publisher -like 'CN=00000000-*')) {
    throw 'installer/msix/store-identity.json still holds placeholders: copy Name, Publisher and Publisher display name from Partner Center > Product identity.'
}

$exePath = (Resolve-Path $Exe).Path
New-Item -ItemType Directory -Force $Out | Out-Null
$msix = Join-Path (Resolve-Path $Out).Path "QuantumWake-$Version-win-x64.msix"

# --- MakeAppx -----------------------------------------------------------------

$cache = Join-Path ([IO.Path]::GetTempPath()) "qw-sdk-buildtools-$buildToolsVersion"
$makeAppx = Join-Path $cache "bin/10.0.28000.0/x64/makeappx.exe"

if (-not (Test-Path $makeAppx)) {
    New-Item -ItemType Directory -Force $cache | Out-Null
    $nupkg = Join-Path $cache 'buildtools.nupkg'
    $url = "https://api.nuget.org/v3-flatcontainer/microsoft.windows.sdk.buildtools/$buildToolsVersion/microsoft.windows.sdk.buildtools.$buildToolsVersion.nupkg"

    Invoke-WebRequest $url -OutFile $nupkg -UseBasicParsing
    $actual = (Get-FileHash $nupkg -Algorithm SHA512).Hash.ToLowerInvariant()
    if ($actual -ne $buildToolsSha512) {
        Remove-Item $nupkg
        throw "Microsoft.Windows.SDK.BuildTools $buildToolsVersion did not match its pinned SHA-512 (got $actual)."
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::ExtractToDirectory($nupkg, $cache)
}

# --- The package's contents ---------------------------------------------------

$stage = Join-Path ([IO.Path]::GetTempPath()) "qw-msix-$([guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Force (Join-Path $stage 'Assets') | Out-Null

try {
    Copy-Item $exePath (Join-Path $stage 'QuantumWake.exe')

    # Values go into XML attributes, so they are escaped rather than trusted.
    $escape = { param($s) [Security.SecurityElement]::Escape([string]$s) }
    $manifest = (Get-Content (Join-Path $PSScriptRoot 'msix/AppxManifest.xml') -Raw) `
        -replace '\{\{IdentityName\}\}', (& $escape $identity.IdentityName) `
        -replace '\{\{Publisher\}\}', (& $escape $identity.Publisher) `
        -replace '\{\{PublisherDisplayName\}\}', (& $escape $identity.PublisherDisplayName) `
        -replace '\{\{Version\}\}', $packageVersion
    if ($manifest -match '\{\{') { throw 'AppxManifest.xml has a token this script does not fill.' }
    [IO.File]::WriteAllText((Join-Path $stage 'AppxManifest.xml'), $manifest, [Text.UTF8Encoding]::new($false))

    # The logos are drawn from the app icon's 256-pixel frame at build time
    # rather than checked in, so they can never drift from the icon the exe
    # and the tray already use.
    Add-Type -AssemblyName System.Drawing
    $icon = [Drawing.Icon]::new((Join-Path $root 'src/Quantumwake.Overlay/app.ico'), 256, 256)
    $source = $icon.ToBitmap()
    try {
        foreach ($logo in @(
                @{ Name = 'StoreLogo.png'; Size = 50 },
                @{ Name = 'Square44x44Logo.png'; Size = 44 },
                @{ Name = 'Square150x150Logo.png'; Size = 150 })) {
            $bitmap = [Drawing.Bitmap]::new($logo.Size, $logo.Size)
            $graphics = [Drawing.Graphics]::FromImage($bitmap)
            $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $graphics.DrawImage($source, 0, 0, $logo.Size, $logo.Size)
            $graphics.Dispose()
            $bitmap.Save((Join-Path $stage "Assets/$($logo.Name)"), [Drawing.Imaging.ImageFormat]::Png)
            $bitmap.Dispose()
        }
    }
    finally {
        $source.Dispose()
        $icon.Dispose()
    }

    # MakeAppx checks the manifest against the schema while packing, so a
    # manifest Windows would refuse to install fails here instead.
    & $makeAppx pack /d $stage /p $msix /o
    if ($LASTEXITCODE -ne 0) { throw 'makeappx pack failed' }
}
finally {
    Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "Built $msix ($([math]::Round((Get-Item $msix).Length / 1MB, 1)) MB, package version $packageVersion)"
