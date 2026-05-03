[CmdletBinding()]
param(
    [string]$PublishPath = "",
    [string]$OutputRoot = ".release",
    [string]$ExpectedVersion = "",
    [switch]$Force,
    [switch]$AllowDirty,
    [switch]$AllowNonMain
)

$ErrorActionPreference = "Stop"

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $RepoRoot

function Get-PmgReleaseVersion {
    $projectPath = Join-Path $RepoRoot "PitmastersGrill/PitmastersGrill.csproj"

    if (-not (Test-Path $projectPath)) {
        throw "Project file not found: $projectPath"
    }

    $projectText = Get-Content $projectPath -Raw

    if ($projectText -notmatch "<PmgReleaseVersion>(?<version>[^<]+)</PmgReleaseVersion>") {
        throw "PmgReleaseVersion was not found in PitmastersGrill.csproj."
    }

    return $Matches["version"].Trim()
}

function Assert-GitState {
    $branch = (git branch --show-current).Trim()
    if ($branch -ne "main" -and -not $AllowNonMain) {
        throw "Current branch is '$branch'. Run package preparation from main, or use -AllowNonMain for development validation."
    }

    $status = git status --porcelain
    if (-not [string]::IsNullOrWhiteSpace(($status -join "")) -and -not $AllowDirty) {
        throw "Working tree has uncommitted changes. Commit/stash first, or use -AllowDirty for development validation."
    }
}

function Resolve-PublishPath {
    param([string]$RequestedPath)

    if (-not [string]::IsNullOrWhiteSpace($RequestedPath)) {
        $resolved = Resolve-Path $RequestedPath -ErrorAction Stop
        if (-not (Test-Path $resolved.Path -PathType Container)) {
            throw "PublishPath is not a directory: $RequestedPath"
        }
        return $resolved.Path
    }

    $candidates = @(
        "PitmastersGrill/bin/Release/net10.0-windows/win-x64/publish",
        "PitmastersGrill/bin/Release/net10.0-windows/publish",
        "PitmastersGrill/bin/Release/net10.0-windows"
    )

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate -PathType Container) {
            return (Resolve-Path $candidate).Path
        }
    }

    throw "Publish output folder was not found. Run Visual Studio/dotnet publish first, or pass -PublishPath <folder>."
}

function Assert-PublishOutput {
    param([string]$ResolvedPublishPath)

    $exe = Join-Path $ResolvedPublishPath "PitmastersGrill.exe"
    $dll = Join-Path $ResolvedPublishPath "PitmastersGrill.dll"

    if ((Test-Path $exe) -or (Test-Path $dll)) {
        return
    }

    throw "Publish output does not contain PitmastersGrill.exe or PitmastersGrill.dll: $ResolvedPublishPath"
}

function New-CleanZip {
    param(
        [string]$SourceDirectory,
        [string]$DestinationZip
    )

    Add-Type -AssemblyName System.IO.Compression.FileSystem

    if (Test-Path $DestinationZip) {
        if ($Force) {
            Remove-Item $DestinationZip -Force
        }
        else {
            throw "ZIP already exists: $DestinationZip. Use -Force to overwrite."
        }
    }

    [System.IO.Compression.ZipFile]::CreateFromDirectory($SourceDirectory, $DestinationZip)
}

function Write-Sha256File {
    param(
        [string]$ArtifactPath,
        [string]$ChecksumPath
    )

    if (Test-Path $ChecksumPath) {
        if ($Force) {
            Remove-Item $ChecksumPath -Force
        }
        else {
            throw "Checksum file already exists: $ChecksumPath. Use -Force to overwrite."
        }
    }

    $hash = Get-FileHash -Algorithm SHA256 -Path $ArtifactPath
    $line = "$($hash.Hash.ToLowerInvariant())  $(Split-Path $ArtifactPath -Leaf)"
    Set-Content -Path $ChecksumPath -Value $line -Encoding UTF8
    return $hash.Hash.ToLowerInvariant()
}

Write-Host "PMG release package preparation"
Write-Host "Repository: $RepoRoot"
Write-Host ""

Assert-GitState

$version = Get-PmgReleaseVersion

if ($version -notmatch "^\d+\.\d+\.\d+$") {
    throw "PmgReleaseVersion must be semantic version major.minor.patch. Found: $version"
}

if (-not [string]::IsNullOrWhiteSpace($ExpectedVersion) -and $ExpectedVersion -ne $version) {
    throw "ExpectedVersion '$ExpectedVersion' does not match PmgReleaseVersion '$version'."
}

$tag = "v$version"
$releaseTitle = "Pitmasters Grill $version"
$zipName = "PMG_General-Release_v$version.zip"

$resolvedPublishPath = Resolve-PublishPath -RequestedPath $PublishPath
Assert-PublishOutput -ResolvedPublishPath $resolvedPublishPath

$outputRootPath = Join-Path $RepoRoot $OutputRoot
$outputVersionPath = Join-Path $outputRootPath $version
New-Item -ItemType Directory -Force -Path $outputVersionPath | Out-Null

$zipPath = Join-Path $outputVersionPath $zipName
$checksumPath = "$zipPath.sha256"

New-CleanZip -SourceDirectory $resolvedPublishPath -DestinationZip $zipPath
$sha256 = Write-Sha256File -ArtifactPath $zipPath -ChecksumPath $checksumPath

$summaryPath = Join-Path $outputVersionPath "release-package-summary.txt"

$summary = @(
    "PMG release package preparation summary",
    "Version: $version",
    "Release title: $releaseTitle",
    "Tag: $tag",
    "ZIP: $zipPath",
    "Checksum: $checksumPath",
    "SHA256: $sha256",
    "Publish source: $resolvedPublishPath",
    "Generated at UTC: $([DateTime]::UtcNow.ToString('u'))",
    "",
    "This helper does not create tags, GitHub releases, uploads, commits, pushes, or merges."
)

Set-Content -Path $summaryPath -Value $summary -Encoding UTF8

Write-Host "Release package prepared."
Write-Host "Version: $version"
Write-Host "Release title: $releaseTitle"
Write-Host "Tag: $tag"
Write-Host "ZIP: $zipPath"
Write-Host "Checksum: $checksumPath"
Write-Host "SHA256: $sha256"
Write-Host "Summary: $summaryPath"
Write-Host ""
Write-Host "No GitHub release, tag, upload, commit, push, or merge was performed."
