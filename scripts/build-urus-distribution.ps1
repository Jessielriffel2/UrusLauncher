[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version = '1.1.0',

    [ValidateSet('win-x64')]
    [string]$RuntimeIdentifier = 'win-x64',

    [string]$InnoCompiler = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",

    [string]$LegacyRuntimeSource = '',

    [switch]$SkipTests,

    [switch]$SkipPortableStartupSmoke
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-CheckedCommand {
    param(
        [Parameter(Mandatory)]
        [string]$FilePath,

        [Parameter(Mandatory)]
        [string[]]$ArgumentList
    )

    & $FilePath @ArgumentList
    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code $LASTEXITCODE`: $FilePath"
    }
}

function Get-VerifiedChildPath {
    param(
        [Parameter(Mandatory)]
        [string]$Candidate,

        [Parameter(Mandatory)]
        [string]$Parent
    )

    $parentPath = [System.IO.Path]::GetFullPath($Parent).TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)
    $candidatePath = [System.IO.Path]::GetFullPath($Candidate)
    $requiredPrefix = $parentPath + [System.IO.Path]::DirectorySeparatorChar
    if (-not $candidatePath.StartsWith(
        $requiredPrefix,
        [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to modify a path outside '$parentPath': '$candidatePath'."
    }

    return $candidatePath
}

function Assert-FileExists {
    param(
        [Parameter(Mandatory)]
        [string]$Path,

        [Parameter(Mandatory)]
        [string]$Description
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Description was not found: '$Path'."
    }
}

function Assert-SelfContainedApplication {
    param(
        [Parameter(Mandatory)]
        [string]$PayloadDirectory,

        [Parameter(Mandatory)]
        [string]$ApplicationName
    )

    $runtimeConfigPath = Join-Path $PayloadDirectory "$ApplicationName.runtimeconfig.json"
    $depsPath = Join-Path $PayloadDirectory "$ApplicationName.deps.json"
    Assert-FileExists $runtimeConfigPath "$ApplicationName runtime configuration"
    Assert-FileExists $depsPath "$ApplicationName dependency manifest"

    $runtimeConfiguration = Get-Content -LiteralPath $runtimeConfigPath -Raw |
        ConvertFrom-Json
    $runtimeProperties = $runtimeConfiguration.runtimeOptions.PSObject.Properties.Name
    if ($runtimeProperties -contains 'framework' -or
        $runtimeProperties -contains 'frameworks' -or
        $runtimeProperties -notcontains 'includedFrameworks') {
        throw "$ApplicationName is not configured as a self-contained application."
    }

    $dependencies = Get-Content -LiteralPath $depsPath -Raw
    if ($dependencies -notmatch 'runtimepack\.Microsoft\.NETCore\.App\.Runtime\.win-x64') {
        throw "$ApplicationName does not contain the win-x64 .NET runtime pack."
    }
}

function Assert-WpfWindowsBase {
    param(
        [Parameter(Mandatory)]
        [string]$PayloadDirectory
    )

    $windowsBasePath = Join-Path $PayloadDirectory 'WindowsBase.dll'
    Assert-FileExists $windowsBasePath 'WPF WindowsBase runtime assembly'
    $windowsBase = Get-Item -LiteralPath $windowsBasePath
    if ($windowsBase.Length -le 1MB) {
        throw "WindowsBase.dll is only $($windowsBase.Length) bytes; the WPF runtime was replaced by a facade assembly."
    }

    $assemblyVersion = [System.Reflection.AssemblyName]::GetAssemblyName(
        $windowsBasePath).Version
    $productVersion = $windowsBase.VersionInfo.ProductVersion
    if ($assemblyVersion.Major -ne 10 -or
        -not $productVersion.StartsWith('10.0.', [System.StringComparison]::Ordinal)) {
        throw "Unexpected WPF WindowsBase.dll version: assembly $assemblyVersion, product $productVersion."
    }

    Write-Host "Validated WPF WindowsBase.dll: $($windowsBase.Length) bytes, version $productVersion."
}

function Resolve-LegacyRuntimeSource {
    param(
        [string]$ExplicitSource
    )

    $candidates = [System.Collections.Generic.List[string]]::new()
    if (-not [string]::IsNullOrWhiteSpace($ExplicitSource)) {
        $candidates.Add($ExplicitSource)
    }
    if (-not [string]::IsNullOrWhiteSpace($env:LEGEND_LEGACY_ROOT)) {
        $candidates.Add($env:LEGEND_LEGACY_ROOT)
    }

    foreach ($programFilesRoot in @($env:ProgramFiles, ${env:ProgramFiles(x86)})) {
        if (-not [string]::IsNullOrWhiteSpace($programFilesRoot)) {
            $candidates.Add((Join-Path $programFilesRoot 'Legend Online Client by Brov (H2_x64)'))
        }
    }

    foreach ($candidate in $candidates) {
        try {
            $fullPath = [System.IO.Path]::GetFullPath($candidate.Trim().Trim('"'))
            if (Test-Path -LiteralPath $fullPath -PathType Container) {
                return $fullPath
            }
        }
        catch [System.ArgumentException], [System.NotSupportedException], [System.IO.PathTooLongException] {
            continue
        }
    }

    throw @'
No authorized legacy runtime source was found. Pass -LegacyRuntimeSource or set
LEGEND_LEGACY_ROOT to a directory containing Adobe.Flash.Control.manifest and
the referenced x64 ActiveX control. The build never downloads an untrusted runtime.
'@
}

function Get-ManifestActiveXPath {
    param(
        [Parameter(Mandatory)]
        [string]$ManifestPath
    )

    $settings = [System.Xml.XmlReaderSettings]::new()
    $settings.DtdProcessing = [System.Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $settings.MaxCharactersInDocument = 1MB
    $reader = [System.Xml.XmlReader]::Create($ManifestPath, $settings)
    try {
        while ($reader.Read()) {
            if ($reader.NodeType -ne [System.Xml.XmlNodeType]::Element -or
                -not $reader.LocalName.Equals('file', [System.StringComparison]::OrdinalIgnoreCase)) {
                continue
            }

            $relativePath = $reader.GetAttribute('name')
            if (-not [string]::IsNullOrWhiteSpace($relativePath) -and
                $relativePath.EndsWith('.ocx', [System.StringComparison]::OrdinalIgnoreCase)) {
                return $relativePath
            }
        }
    }
    finally {
        $reader.Dispose()
    }

    throw "The Flash manifest does not reference an ActiveX .ocx file: '$ManifestPath'."
}

function Copy-LegacyRuntimePayload {
    param(
        [Parameter(Mandatory)]
        [string]$SourceDirectory,

        [Parameter(Mandatory)]
        [string]$PayloadDirectory
    )

    $manifestSource = Join-Path $SourceDirectory 'Adobe.Flash.Control.manifest'
    Assert-FileExists $manifestSource 'Legacy Flash activation manifest'
    $activeXRelativePath = Get-ManifestActiveXPath $manifestSource
    if ([System.IO.Path]::IsPathRooted($activeXRelativePath)) {
        throw "The Flash manifest references an absolute path: '$activeXRelativePath'."
    }

    $activeXSource = Get-VerifiedChildPath `
        (Join-Path $SourceDirectory $activeXRelativePath) `
        $SourceDirectory
    Assert-FileExists $activeXSource 'Legacy Flash ActiveX control'
    $activeXFile = Get-Item -LiteralPath $activeXSource
    if ($activeXFile.Length -le 1MB) {
        throw "The Flash ActiveX control is unexpectedly small: $($activeXFile.Length) bytes."
    }

    $signature = Get-AuthenticodeSignature -LiteralPath $activeXSource
    if ($signature.Status -ne [System.Management.Automation.SignatureStatus]::Valid) {
        throw "The Flash ActiveX signature is not valid: $($signature.Status)."
    }

    $runtimeDestination = Get-VerifiedChildPath `
        (Join-Path $PayloadDirectory 'runtime') `
        $PayloadDirectory
    $manifestDestination = Join-Path $runtimeDestination 'Adobe.Flash.Control.manifest'
    $activeXDestination = Get-VerifiedChildPath `
        (Join-Path $runtimeDestination $activeXRelativePath) `
        $runtimeDestination
    New-Item -ItemType Directory -Path (Split-Path $activeXDestination -Parent) -Force |
        Out-Null
    Copy-Item -LiteralPath $manifestSource -Destination $manifestDestination -Force
    Copy-Item -LiteralPath $activeXSource -Destination $activeXDestination -Force

    Assert-FileExists $manifestDestination 'Bundled Flash activation manifest'
    Assert-FileExists $activeXDestination 'Bundled Flash ActiveX control'
    Write-Host "Bundled the validated registration-free runtime in '$runtimeDestination'."

    return [pscustomobject]@{
        Directory = $runtimeDestination
        Manifest = $manifestDestination
        ActiveX = $activeXDestination
        ActiveXRelativePath = $activeXRelativePath
    }
}

function Test-PortableLauncherStartup {
    param(
        [Parameter(Mandatory)]
        [string]$PayloadDirectory,

        [TimeSpan]$ObservationTime = [TimeSpan]::FromSeconds(7)
    )

    $executable = Join-Path $PayloadDirectory 'UrusLauncher.App.exe'
    Assert-FileExists $executable 'Portable Urus Launcher executable'
    $previousDotnetRoot = $env:DOTNET_ROOT
    $previousMultilevelLookup = $env:DOTNET_MULTILEVEL_LOOKUP
    $process = $null
    try {
        $env:DOTNET_ROOT = 'C:\__urus_missing_dotnet_runtime__'
        $env:DOTNET_MULTILEVEL_LOOKUP = '0'
        $process = Start-Process `
            -FilePath $executable `
            -WorkingDirectory $PayloadDirectory `
            -WindowStyle Hidden `
            -PassThru
        if ($process.WaitForExit([int]$ObservationTime.TotalMilliseconds)) {
            throw "Portable Urus Launcher exited during startup with code $($process.ExitCode)."
        }

        $process.Refresh()
        if ($process.HasExited) {
            throw "Portable Urus Launcher exited before the startup observation completed."
        }

        $loadedModules = @($process.Modules | ForEach-Object ModuleName)
        $missingModules = @(
            @(
                'WindowsBase.dll',
                'PresentationFramework.dll'
            ) | Where-Object { $_ -notin $loadedModules }
        )
        if (-not $process.Responding -or
            $process.MainWindowHandle -eq [IntPtr]::Zero -or
            $process.MainWindowTitle -ne 'Urus Launcher' -or
            $missingModules.Count -gt 0) {
            throw "Portable startup did not reach the Urus Launcher window. Title='$($process.MainWindowTitle)'; missing modules='$($missingModules -join ', ')'."
        }

        Write-Host "Portable startup smoke reached '$($process.MainWindowTitle)' for $($ObservationTime.TotalSeconds) seconds without a global .NET runtime."
    }
    finally {
        if ($null -ne $process -and -not $process.HasExited) {
            Stop-Process -Id $process.Id -Force
            $process.WaitForExit(5000) | Out-Null
        }

        $env:DOTNET_ROOT = $previousDotnetRoot
        $env:DOTNET_MULTILEVEL_LOOKUP = $previousMultilevelLookup
    }
}

function Get-ArtifactRecord {
    param(
        [Parameter(Mandatory)]
        [System.IO.FileInfo]$File
    )

    $stream = [System.IO.File]::OpenRead($File.FullName)
    $algorithm = [System.Security.Cryptography.SHA256]::Create()
    try {
        $hashBytes = $algorithm.ComputeHash($stream)
        $hash = [System.BitConverter]::ToString($hashBytes).Replace('-', '')
    }
    finally {
        $algorithm.Dispose()
        $stream.Dispose()
    }

    return [ordered]@{
        file = $File.Name
        bytes = $File.Length
        sha256 = $hash
    }
}

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactsRoot = Join-Path $repositoryRoot 'artifacts'
$distributionRoot = Get-VerifiedChildPath `
    (Join-Path $artifactsRoot 'urus-distribution') `
    $artifactsRoot
$portableRoot = Get-VerifiedChildPath `
    (Join-Path $distributionRoot 'portable') `
    $distributionRoot
$payloadDirectory = Get-VerifiedChildPath `
    (Join-Path $portableRoot 'UrusLauncher') `
    $portableRoot
$stagingRoot = Get-VerifiedChildPath `
    (Join-Path $distributionRoot '.staging') `
    $distributionRoot
$gameHostStaging = Get-VerifiedChildPath `
    (Join-Path $stagingRoot 'gamehost') `
    $stagingRoot

$solutionPath = Join-Path $repositoryRoot 'LegendLauncherNext.slnx'
$appProject = Join-Path $repositoryRoot 'src\LegendLauncher.App\LegendLauncher.App.csproj'
$gameHostProject = Join-Path $repositoryRoot 'src\LegendLauncher.GameHost.Legacy\LegendLauncher.GameHost.Legacy.csproj'
$installerScript = Join-Path $repositoryRoot 'installer\UrusLauncher.iss'
$brandingIcon = Join-Path $repositoryRoot 'src\LegendLauncher.App\Assets\Branding\urus-launcher.ico'
$releaseDefinitionPath = Join-Path $repositoryRoot "docs\releases\v$Version.json"
$dotnet = (Get-Command dotnet -ErrorAction Stop).Source

Assert-FileExists $solutionPath 'Solution'
Assert-FileExists $appProject 'Urus Launcher application project'
Assert-FileExists $gameHostProject 'GameHost project'
Assert-FileExists $installerScript 'Inno Setup script'
Assert-FileExists $brandingIcon 'Urus Launcher icon'
Assert-FileExists $releaseDefinitionPath "Release definition for version $Version"
Assert-FileExists $InnoCompiler 'Inno Setup compiler'
$resolvedLegacyRuntimeSource = Resolve-LegacyRuntimeSource $LegacyRuntimeSource
Write-Host "Using the builder-supplied legacy runtime from '$resolvedLegacyRuntimeSource'."

$releaseDefinition = Get-Content -LiteralPath $releaseDefinitionPath -Raw |
    ConvertFrom-Json
if ($releaseDefinition.schemaVersion -ne 1 -or
    $releaseDefinition.version -ne $Version) {
    throw "Release definition must use schemaVersion 1 and version '$Version'."
}

$releaseLanguages = @('pt-BR', 'en-US', 'es-ES')
$localizedNotes = [ordered]@{}
foreach ($languageCode in $releaseLanguages) {
    $titleProperty = $releaseDefinition.title.PSObject.Properties[$languageCode]
    $notesProperty = $releaseDefinition.notes.PSObject.Properties[$languageCode]
    if ($null -eq $titleProperty -or
        [string]::IsNullOrWhiteSpace([string]$titleProperty.Value) -or
        $null -eq $notesProperty) {
        throw "Release definition is missing localized content for '$languageCode'."
    }

    $noteLines = @($notesProperty.Value | ForEach-Object { [string]$_ })
    if ($noteLines.Count -eq 0 -or
        @($noteLines | Where-Object { [string]::IsNullOrWhiteSpace($_) }).Count -gt 0) {
        throw "Release notes for '$languageCode' must contain non-empty lines."
    }

    $localizedNotes[$languageCode] =
        ($noteLines | ForEach-Object { "- $_" }) -join [Environment]::NewLine
}

if (Test-Path -LiteralPath $distributionRoot) {
    Write-Host "Removing verified distribution directory: $distributionRoot"
    Remove-Item -LiteralPath $distributionRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $payloadDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $gameHostStaging -Force | Out-Null

Push-Location $repositoryRoot
try {
    if (-not $SkipTests) {
        Write-Host 'Running the Release test suite...'
        Invoke-CheckedCommand $dotnet @(
            'test',
            $solutionPath,
            '--configuration', 'Release',
            '--nologo'
        )
    }

    $publishProperties = @(
        '-p:DebugSymbols=false',
        '-p:DebugType=None',
        '-p:PublishTrimmed=false',
        '-p:UseAppHost=true'
    )

    Write-Host 'Publishing Urus Launcher as self-contained win-x64...'
    Invoke-CheckedCommand $dotnet @(
        'publish',
        $appProject,
        '--configuration', 'Release',
        '--runtime', $RuntimeIdentifier,
        '--self-contained', 'true',
        '--output', $payloadDirectory,
        '--nologo',
        "-p:Version=$Version"
        $publishProperties
    )

    Write-Host 'Publishing the isolated GameHost as self-contained win-x64...'
    Invoke-CheckedCommand $dotnet @(
        'publish',
        $gameHostProject,
        '--configuration', 'Release',
        '--runtime', $RuntimeIdentifier,
        '--self-contained', 'true',
        '--output', $gameHostStaging,
        '--nologo',
        "-p:Version=$Version"
        $publishProperties
    )
}
finally {
    Pop-Location
}

$gameHostPayloadFiles = @(
    'LegendLauncher.GameHost.Legacy.exe',
    'LegendLauncher.GameHost.Legacy.dll',
    'LegendLauncher.GameHost.Legacy.deps.json',
    'LegendLauncher.GameHost.Legacy.runtimeconfig.json'
)
foreach ($fileName in $gameHostPayloadFiles) {
    $sourcePath = Join-Path $gameHostStaging $fileName
    Assert-FileExists $sourcePath "Self-contained GameHost file '$fileName'"
    Copy-Item -LiteralPath $sourcePath -Destination $payloadDirectory -Force
}
Copy-Item -LiteralPath $brandingIcon `
    -Destination (Join-Path $payloadDirectory 'urus-launcher.ico') `
    -Force
$bundledLegacyRuntime = Copy-LegacyRuntimePayload `
    $resolvedLegacyRuntimeSource `
    $payloadDirectory
Remove-Item -LiteralPath $stagingRoot -Recurse -Force

$mainExecutable = Join-Path $payloadDirectory 'UrusLauncher.App.exe'
$gameHostExecutable = Join-Path $payloadDirectory 'LegendLauncher.GameHost.Legacy.exe'
Assert-FileExists $mainExecutable 'Main Urus Launcher executable'
Assert-FileExists $gameHostExecutable 'Isolated GameHost executable'
Assert-FileExists (Join-Path $payloadDirectory 'coreclr.dll') '.NET Core runtime'
Assert-FileExists (Join-Path $payloadDirectory 'hostfxr.dll') '.NET host resolver'
Assert-FileExists (Join-Path $payloadDirectory 'PresentationFramework.dll') 'WPF runtime'
Assert-FileExists (Join-Path $payloadDirectory 'System.Windows.Forms.dll') 'Windows Forms runtime'
Assert-WpfWindowsBase $payloadDirectory
if (Test-Path -LiteralPath (Join-Path $payloadDirectory 'LegendLauncher.App.exe')) {
    throw 'The obsolete LegendLauncher.App.exe was found in the Urus payload.'
}

Assert-SelfContainedApplication $payloadDirectory 'UrusLauncher.App'
Assert-SelfContainedApplication $payloadDirectory 'LegendLauncher.GameHost.Legacy'
if (-not $SkipPortableStartupSmoke) {
    Test-PortableLauncherStartup $payloadDirectory
}

$previousProductVersion = $env:URUS_LAUNCHER_VERSION
$previousFileVersion = $env:URUS_LAUNCHER_FILE_VERSION
$versionParts = $Version.Split('.')
$fileVersion = if ($versionParts.Count -eq 3) { "$Version.0" } else { $Version }
try {
    $env:URUS_LAUNCHER_VERSION = $Version
    $env:URUS_LAUNCHER_FILE_VERSION = $fileVersion
    Write-Host 'Compiling the per-user Inno Setup installer...'
    Invoke-CheckedCommand $InnoCompiler @('/Qp', $installerScript)
}
finally {
    $env:URUS_LAUNCHER_VERSION = $previousProductVersion
    $env:URUS_LAUNCHER_FILE_VERSION = $previousFileVersion
}

$installerPath = Join-Path $distributionRoot "UrusLauncher-Setup-$Version-win-x64.exe"
Assert-FileExists $installerPath 'Compiled Urus Launcher installer'

$portableZipPath = Join-Path $distributionRoot "UrusLauncher-$Version-portable-win-x64.zip"
Write-Host 'Creating the portable ZIP...'
Compress-Archive `
    -LiteralPath $payloadDirectory `
    -DestinationPath $portableZipPath `
    -CompressionLevel Optimal `
    -Force
Assert-FileExists $portableZipPath 'Portable Urus Launcher ZIP'

$payloadFiles = @(Get-ChildItem -LiteralPath $payloadDirectory -File -Recurse)
$payloadBytes = ($payloadFiles | Measure-Object -Property Length -Sum).Sum
$installerRecord = Get-ArtifactRecord (Get-Item -LiteralPath $installerPath)
$portableRecord = Get-ArtifactRecord (Get-Item -LiteralPath $portableZipPath)
$runtimeManifestRecord = Get-ArtifactRecord (Get-Item -LiteralPath $bundledLegacyRuntime.Manifest)
$runtimeActiveXRecord = Get-ArtifactRecord (Get-Item -LiteralPath $bundledLegacyRuntime.ActiveX)
$updateManifestPath = Join-Path $distributionRoot 'update-manifest.json'
$updateManifest = [ordered]@{
    schema = 1
    repository = 'Jessielriffel2/UrusLauncher'
    version = $Version
    installer = [ordered]@{
        name = $installerRecord.file
        bytes = $installerRecord.bytes
        sha256 = $installerRecord.sha256
    }
    notes = $localizedNotes
}
$updateManifest | ConvertTo-Json -Depth 5 |
    Set-Content -LiteralPath $updateManifestPath -Encoding utf8

$releaseNotesPath = Join-Path $distributionRoot 'RELEASE_NOTES.md'
$releaseNotesMarkdown = [System.Collections.Generic.List[string]]::new()
$releaseNotesMarkdown.Add("# Urus Launcher $Version")
$releaseNotesMarkdown.Add('')
foreach ($languageCode in $releaseLanguages) {
    $releaseNotesMarkdown.Add("## $($releaseDefinition.title.PSObject.Properties[$languageCode].Value) ($languageCode)")
    $releaseNotesMarkdown.Add('')
    foreach ($noteLine in @($releaseDefinition.notes.PSObject.Properties[$languageCode].Value)) {
        $releaseNotesMarkdown.Add("- $noteLine")
    }
    $releaseNotesMarkdown.Add('')
}
$releaseNotesMarkdown | Set-Content -LiteralPath $releaseNotesPath -Encoding utf8

$updateManifestRecord = Get-ArtifactRecord (Get-Item -LiteralPath $updateManifestPath)
$releaseNotesRecord = Get-ArtifactRecord (Get-Item -LiteralPath $releaseNotesPath)
$manifest = [ordered]@{
    product = 'Urus Launcher'
    version = $Version
    runtimeIdentifier = $RuntimeIdentifier
    selfContained = $true
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    installer = $installerRecord
    portableZip = $portableRecord
    updater = [ordered]@{
        manifest = $updateManifestRecord
        releaseNotes = $releaseNotesRecord
    }
    legacyRuntime = [ordered]@{
        bundled = $true
        directory = 'runtime'
        manifest = $runtimeManifestRecord
        activeX = [ordered]@{
            path = $bundledLegacyRuntime.ActiveXRelativePath
            file = $runtimeActiveXRecord.file
            bytes = $runtimeActiveXRecord.bytes
            sha256 = $runtimeActiveXRecord.sha256
        }
    }
    payload = [ordered]@{
        directory = 'portable/UrusLauncher'
        files = $payloadFiles.Count
        bytes = $payloadBytes
        mainExecutable = 'UrusLauncher.App.exe'
        gameHostExecutable = 'LegendLauncher.GameHost.Legacy.exe'
    }
}

$manifestPath = Join-Path $distributionRoot 'distribution-manifest.json'
$manifest | ConvertTo-Json -Depth 5 |
    Set-Content -LiteralPath $manifestPath -Encoding utf8
$checksumsPath = Join-Path $distributionRoot 'SHA256SUMS.txt'
@(
    "$($installerRecord.sha256)  $($installerRecord.file)",
    "$($portableRecord.sha256)  $($portableRecord.file)",
    "$($updateManifestRecord.sha256)  $($updateManifestRecord.file)",
    "$($releaseNotesRecord.sha256)  $($releaseNotesRecord.file)"
) | Set-Content -LiteralPath $checksumsPath -Encoding ascii

Write-Host ''
Write-Host 'Urus Launcher distribution completed successfully.'
Write-Host "Installer: $installerPath"
Write-Host "Portable ZIP: $portableZipPath"
Write-Host "Update manifest: $updateManifestPath"
Write-Host "Release notes: $releaseNotesPath"
Write-Host "Payload files: $($payloadFiles.Count)"
Write-Host "Payload bytes: $payloadBytes"
Write-Host "Checksums: $checksumsPath"
