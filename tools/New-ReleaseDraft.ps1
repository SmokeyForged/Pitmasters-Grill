[CmdletBinding()]
param(
    [string]$PackageRoot = ".release",
    [string]$ReleaseNotesPath = "",
    [string]$ExpectedVersion = "",
    [switch]$CreateDraft,
    [switch]$AllowDirty,
    [switch]$AllowNonMain
)

$ErrorActionPreference = "Stop"

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $RepoRoot

function Invoke-Capture {
    param(
        [string]$FilePath,
        [string[]]$Arguments
    )

    $output = & $FilePath @Arguments 2>&1
    $exitCode = $LASTEXITCODE

    return [pscustomobject]@{
        Output = ($output -join [Environment]::NewLine)
        ExitCode = $exitCode
    }
}

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
        throw "Current branch is '$branch'. Run release draft creation from main, or use -AllowNonMain for development validation."
    }

    $status = git status --porcelain
    if (-not [string]::IsNullOrWhiteSpace(($status -join "")) -and -not $AllowDirty) {
        throw "Working tree has uncommitted changes. Commit/stash first, or use -AllowDirty for development validation."
    }
}

function Assert-GhAvailable {
    $version = Invoke-Capture -FilePath "gh" -Arguments @("--version")
    if ($version.ExitCode -ne 0) {
        throw "GitHub CLI is not available or not authenticated. gh output: $($version.Output)"
    }

    $auth = Invoke-Capture -FilePath "gh" -Arguments @("auth", "status")
    if ($auth.ExitCode -ne 0) {
        throw "GitHub CLI authentication check failed. gh output: $($auth.Output)"
    }
}

function Resolve-ReleaseNotesPath {
    param(
        [string]$RequestedPath,
        [string]$Version
    )

    if (-not [string]::IsNullOrWhiteSpace($RequestedPath)) {
        $resolved = Resolve-Path $RequestedPath -ErrorAction Stop
        return $resolved.Path
    }

    $candidate = Join-Path "Patch Notes" ("General-Release_{0}.md" -f $Version.Replace(".", "-"))

    if (Test-Path $candidate) {
        return (Resolve-Path $candidate).Path
    }

    throw "Release notes file was not found. Expected $candidate or pass -ReleaseNotesPath <file>."
}

function Assert-FilePresent {
    param(
        [string]$Path,
        [string]$Description
    )

    if (-not (Test-Path $Path -PathType Leaf)) {
        throw "$Description missing: $Path"
    }
}

function Get-ExistingRelease {
    param(
        [string]$Tag
    )

    $view = Invoke-Capture -FilePath "gh" -Arguments @(
        "release", "view", $Tag,
        "--repo", "SmokeyForged/Pitmasters-Grill",
        "--json", "tagName,name,isDraft,isPrerelease,url"
    )

    if ($view.ExitCode -ne 0) {
        return $null
    }

    return ($view.Output | ConvertFrom-Json)
}

Write-Host "PMG GitHub release draft helper"
Write-Host "Repository: $RepoRoot"
Write-Host ""

Assert-GitState
Assert-GhAvailable

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

$packageVersionPath = Join-Path $PackageRoot $version
$zipPath = Join-Path $packageVersionPath $zipName
$checksumPath = "$zipPath.sha256"
$summaryPath = Join-Path $packageVersionPath "release-package-summary.txt"
$notesPath = Resolve-ReleaseNotesPath -RequestedPath $ReleaseNotesPath -Version $version

Assert-FilePresent -Path $zipPath -Description "Release ZIP"
Assert-FilePresent -Path $checksumPath -Description "SHA256 checksum"
Assert-FilePresent -Path $summaryPath -Description "Package preparation summary"
Assert-FilePresent -Path $notesPath -Description "Release notes"

$existing = Get-ExistingRelease -Tag $tag

if ($existing) {
    throw "Release '$tag' already exists on GitHub. Existing release URL: $($existing.url). This helper does not overwrite releases."
}

Write-Host "Release draft inputs verified."
Write-Host "Version: $version"
Write-Host "Release title: $releaseTitle"
Write-Host "Tag: $tag"
Write-Host "ZIP: $zipPath"
Write-Host "Checksum: $checksumPath"
Write-Host "Package summary: $summaryPath"
Write-Host "Release notes: $notesPath"
Write-Host ""

if (-not $CreateDraft) {
    Write-Host "DRY RUN ONLY. No GitHub release was created."
    Write-Host "To create a draft release, rerun with -CreateDraft."
    Write-Host ""
    Write-Host "Planned command:"
    Write-Host "gh release create $tag `"$zipPath`" `"$checksumPath`" --repo SmokeyForged/Pitmasters-Grill --draft --title `"$releaseTitle`" --notes-file `"$notesPath`""
    exit 0
}

Write-Host "Creating GitHub draft release..."
gh release create $tag `
    $zipPath `
    $checksumPath `
    --repo SmokeyForged/Pitmasters-Grill `
    --draft `
    --title $releaseTitle `
    --notes-file $notesPath

Write-Host ""
Write-Host "Draft release created. Verifying..."
gh release view $tag `
    --repo SmokeyForged/Pitmasters-Grill `
    --json tagName,name,isDraft,isPrerelease,url,assets `
    --jq '.'

Write-Host ""
Write-Host "Draft release created only. It was not published."
