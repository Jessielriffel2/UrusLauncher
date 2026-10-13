[CmdletBinding()]
param(
    # Version stamped on the developer build (shown as "Versão X (dev)").
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version = '1.1.21',

    # Copies the build over the per-user installation (close every Urus window first).
    [switch]$Install
)

# Developer build for the maintainer's PC: SwordOnline + the local provider (localhost).
# It is NEVER a release: the public pipeline is scripts/build-urus-distribution.ps1, which forces
# -p:IncludeLocalProvider=false and refuses any assembly that is not marked as a public build.

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$devRoot = Join-Path $repositoryRoot 'artifacts\urus-dev'
$payload = Join-Path $devRoot 'UrusLauncher'
$gameHostStaging = Join-Path $devRoot '.gamehost'
$appProject = Join-Path $repositoryRoot 'src\LegendLauncher.App\LegendLauncher.App.csproj'
$gameHostProject = Join-Path $repositoryRoot 'src\LegendLauncher.GameHost.Legacy\LegendLauncher.GameHost.Legacy.csproj'
$icon = Join-Path $repositoryRoot 'src\LegendLauncher.App\Assets\Branding\urus-launcher.ico'
$installDirectory = Join-Path $env:LOCALAPPDATA 'Programs\Urus Launcher'

if (-not (Test-Path -LiteralPath (Join-Path $repositoryRoot 'src\LegendLauncher.Providers.Local\LegendLauncher.Providers.Local.csproj'))) {
    throw 'The local provider sources were not found: this is the maintainer-only developer build.'
}

if ($Install) {
    $running = @(Get-Process -ErrorAction SilentlyContinue |
        Where-Object { $_.ProcessName -like 'UrusLauncher*' -or $_.ProcessName -like 'LegendLauncher.GameHost*' })
    if ($running.Count -gt 0) {
        throw 'Close the Urus Launcher (and any open game session) before installing the developer build.'
    }
}

$fileVersion = "$Version.0"
$properties = @(
    '-p:IncludeLocalProvider=true',
    '-p:DebugSymbols=false',
    '-p:DebugType=None',
    '-p:PublishTrimmed=false',
    '-p:UseAppHost=true',
    "-p:Version=$Version",
    "-p:AssemblyVersion=$fileVersion",
    "-p:FileVersion=$fileVersion",
    "-p:InformationalVersion=$Version-dev"
)

if (Test-Path -LiteralPath $devRoot) {
    Remove-Item -LiteralPath $devRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $payload, $gameHostStaging -Force | Out-Null

function Invoke-Dotnet {
    param([string[]]$Arguments)
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet failed with exit code $LASTEXITCODE"
    }
}

Write-Host 'Publishing the developer build (SwordOnline + local)...'
Invoke-Dotnet (@('publish', $appProject, '--configuration', 'Release', '--runtime', 'win-x64',
        '--self-contained', 'true', '--output', $payload, '--nologo') + $properties)
Invoke-Dotnet (@('publish', $gameHostProject, '--configuration', 'Release', '--runtime', 'win-x64',
        '--self-contained', 'true', '--output', $gameHostStaging, '--nologo') + $properties)

foreach ($name in 'LegendLauncher.GameHost.Legacy.exe', 'LegendLauncher.GameHost.Legacy.dll',
    'LegendLauncher.GameHost.Legacy.deps.json', 'LegendLauncher.GameHost.Legacy.runtimeconfig.json') {
    Copy-Item -LiteralPath (Join-Path $gameHostStaging $name) -Destination $payload -Force
}

Copy-Item -LiteralPath $icon -Destination (Join-Path $payload 'urus-launcher.ico') -Force
Remove-Item -LiteralPath $gameHostStaging -Recurse -Force

# Sanity: this must really be the developer build.
if (-not (Test-Path -LiteralPath (Join-Path $payload 'LegendLauncher.Providers.Local.dll'))) {
    throw 'The local provider is missing from the developer build.'
}

foreach ($unwanted in 'LegendLauncher.Providers.SevenWan.dll', 'LegendLauncher.Providers.Elarionis.dll') {
    if (Test-Path -LiteralPath (Join-Path $payload $unwanted)) {
        throw "$unwanted must not be part of the build."
    }
}

Write-Host "Developer build ready: $payload"

if ($Install) {
    if (-not (Test-Path -LiteralPath $installDirectory)) {
        throw "The installation directory does not exist: $installDirectory (install the public build once first)."
    }

    # Copies over the installation; the runtime\ folder of the installer is left untouched.
    & robocopy $payload $installDirectory /E /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) {
        throw "robocopy failed with exit code $LASTEXITCODE"
    }

    foreach ($stale in 'LegendLauncher.Providers.SevenWan.dll', 'LegendLauncher.Providers.Elarionis.dll') {
        Remove-Item -LiteralPath (Join-Path $installDirectory $stale) -Force -ErrorAction SilentlyContinue
    }

    Write-Host "Installed over: $installDirectory"
}
