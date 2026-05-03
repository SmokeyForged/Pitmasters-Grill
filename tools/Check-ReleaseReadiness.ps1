[CmdletBinding()]
param(
    [string]$ExpectedVersion = "",
    [switch]$AllowDirty,
    [switch]$AllowNonMain,
    [switch]$SkipDotnetChecks
)

$ErrorActionPreference = "Stop"

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $RepoRoot

$Checks = New-Object System.Collections.Generic.List[object]

function Add-Result {
    param(
        [string]$Name,
        [ValidateSet("PASS", "FAIL", "WARN")]
        [string]$Status,
        [string]$Detail
    )

    $Checks.Add([pscustomobject]@{
        Name = $Name
        Status = $Status
        Detail = $Detail
    })
}

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

function Test-FileContains {
    param(
        [string]$Path,
        [string]$Pattern,
        [string]$Name
    )

    if (-not (Test-Path $Path)) {
        Add-Result $Name "FAIL" "Missing file: $Path"
        return
    }

    $text = Get-Content $Path -Raw

    if ($text -match $Pattern) {
        Add-Result $Name "PASS" "Matched expected content."
    }
    else {
        Add-Result $Name "FAIL" "Expected content not found in $Path."
    }
}

Write-Host "PMG release readiness check"
Write-Host "Repository: $RepoRoot"
Write-Host ""

$version = $null

try {
    $gitRoot = (git rev-parse --show-toplevel).Trim()
    Add-Result "Git repository root" "PASS" $gitRoot
}
catch {
    Add-Result "Git repository root" "FAIL" $_.Exception.Message
}

try {
    $branch = (git branch --show-current).Trim()
    if ($branch -eq "main" -or $AllowNonMain) {
        $detail = if ($branch -eq "main") { "Current branch is main." } else { "Current branch is '$branch' and AllowNonMain was supplied." }
        Add-Result "Branch check" "PASS" $detail
    }
    else {
        Add-Result "Branch check" "FAIL" "Current branch is '$branch'. Release readiness should run on main unless -AllowNonMain is supplied for development validation."
    }
}
catch {
    Add-Result "Branch check" "FAIL" $_.Exception.Message
}

try {
    $status = (git status --porcelain)
    if ([string]::IsNullOrWhiteSpace(($status -join "")) -or $AllowDirty) {
        $detail = if ([string]::IsNullOrWhiteSpace(($status -join ""))) { "Working tree is clean." } else { "Working tree is dirty and AllowDirty was supplied." }
        Add-Result "Working tree" "PASS" $detail
    }
    else {
        Add-Result "Working tree" "FAIL" "Working tree has uncommitted changes."
    }
}
catch {
    Add-Result "Working tree" "FAIL" $_.Exception.Message
}

try {
    $version = Get-PmgReleaseVersion

    if ($version -match "^\d+\.\d+\.\d+$") {
        Add-Result "PmgReleaseVersion format" "PASS" $version
    }
    else {
        Add-Result "PmgReleaseVersion format" "FAIL" "Expected semantic version major.minor.patch, got '$version'."
    }

    if (-not [string]::IsNullOrWhiteSpace($ExpectedVersion)) {
        if ($ExpectedVersion -eq $version) {
            Add-Result "Expected version" "PASS" "ExpectedVersion matches PmgReleaseVersion."
        }
        else {
            Add-Result "Expected version" "FAIL" "ExpectedVersion '$ExpectedVersion' does not match PmgReleaseVersion '$version'."
        }
    }
}
catch {
    Add-Result "PmgReleaseVersion" "FAIL" $_.Exception.Message
}

if ($version) {
    $tag = "v$version"
    $releaseTitle = "Pitmasters Grill $version"
    $zipName = "PMG_General-Release_v$version.zip"
    $patchNotesName = "General-Release_$($version.Replace('.', '-')).md"
    $patchNotesPath = Join-Path "Patch Notes" $patchNotesName

    Add-Result "Derived release title" "PASS" $releaseTitle
    Add-Result "Derived release tag" "PASS" $tag
    Add-Result "Derived release ZIP" "PASS" $zipName

    Test-FileContains -Path "README.md" -Pattern ([regex]::Escape($version)) -Name "README current release contains version"

    if (Test-Path $patchNotesPath) {
        Add-Result "Patch notes file" "PASS" $patchNotesPath
    }
    else {
        Add-Result "Patch notes file" "FAIL" "Missing expected patch notes file: $patchNotesPath"
    }

    Test-FileContains -Path "docs/RELEASE-AUTOMATION-DESIGN.md" -Pattern "PMG_General-Release_v<major>\.<minor>\.<patch>\.zip" -Name "Canonical ZIP pattern documented"
    Test-FileContains -Path "docs/RELEASE-CHECKLIST.md" -Pattern "PmgReleaseVersion" -Name "Release checklist references PmgReleaseVersion"
}

try {
    $diffCheck = Invoke-Capture -FilePath "git" -Arguments @("-c", "core.autocrlf=false", "--no-pager", "diff", "--check")
    if ($diffCheck.ExitCode -eq 0) {
        Add-Result "git diff --check" "PASS" "No whitespace errors detected."
    }
    else {
        Add-Result "git diff --check" "FAIL" $diffCheck.Output
    }
}
catch {
    Add-Result "git diff --check" "FAIL" $_.Exception.Message
}

if ($SkipDotnetChecks) {
    Add-Result "dotnet checks" "WARN" "Skipped because -SkipDotnetChecks was supplied."
}
else {
    $test = Invoke-Capture -FilePath "dotnet" -Arguments @("test", ".\PitmastersGrill.Tests\PitmastersGrill.Tests.csproj")
    if ($test.ExitCode -eq 0) {
        Add-Result "dotnet test" "PASS" "Tests passed."
    }
    else {
        Add-Result "dotnet test" "FAIL" $test.Output
    }

    $build = Invoke-Capture -FilePath "dotnet" -Arguments @("build", ".\PitmastersGrill\PitmastersGrill.csproj")
    if ($build.ExitCode -eq 0) {
        Add-Result "dotnet build" "PASS" "Build succeeded."
    }
    else {
        Add-Result "dotnet build" "FAIL" $build.Output
    }
}

Write-Host ""
Write-Host "Release readiness summary"
Write-Host "-------------------------"

foreach ($check in $Checks) {
    $prefix = switch ($check.Status) {
        "PASS" { "[PASS]" }
        "FAIL" { "[FAIL]" }
        "WARN" { "[WARN]" }
    }

    Write-Host "$prefix $($check.Name): $($check.Detail)"
}

$failures = @($Checks | Where-Object { $_.Status -eq "FAIL" })
$warnings = @($Checks | Where-Object { $_.Status -eq "WARN" })

Write-Host ""
Write-Host "Checks: $($Checks.Count), Failures: $($failures.Count), Warnings: $($warnings.Count)"

if ($failures.Count -gt 0) {
    Write-Host "Release readiness check FAILED." -ForegroundColor Red
    exit 1
}

Write-Host "Release readiness check PASSED." -ForegroundColor Green
exit 0
