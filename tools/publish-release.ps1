param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x86",
    [string]$OutputRoot = "",
    [switch]$SelfContained
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot
$projectPath = Join-Path $repoRoot "PitmastersGrill\PitmastersGrill.csproj"

if (-not (Test-Path $projectPath)) {
    throw "Project file not found: $projectPath"
}

if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repoRoot "artifacts\release"
}

[xml]$projectXml = Get-Content -Path $projectPath
$version = $projectXml.Project.PropertyGroup.Version
if ([string]::IsNullOrWhiteSpace($version)) {
    $version = "0.0.0"
}

$versionForFile = $version.Replace(".", "_")
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$publishDir = Join-Path $OutputRoot "publish-$versionForFile-$Runtime-$timestamp"
$zipPath = Join-Path $OutputRoot "PMG-tech-preview-v$versionForFile.zip"
$checksumPath = "$zipPath.sha256.txt"
$releaseNotesPath = Join-Path $OutputRoot "PMG-tech-preview-v$versionForFile-release-notes-template.md"

New-Item -ItemType Directory -Force -Path $OutputRoot | Out-Null
if (Test-Path $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $publishDir | Out-Null

$selfContainedValue = if ($SelfContained.IsPresent) { "true" } else { "false" }

dotnet publish $projectPath `
    -c $Configuration `
    -r $Runtime `
    --self-contained $selfContainedValue `
    -o $publishDir

if (Test-Path $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -Force

$hash = Get-FileHash -Path $zipPath -Algorithm SHA256
"$($hash.Hash)  $(Split-Path -Leaf $zipPath)" | Set-Content -Path $checksumPath -Encoding UTF8

@"
# PMG Technical Preview v$version

## Summary
- 

## Validation
- Build:
- Launch:
- Clipboard/local-list parse:
- Resolver/cache behavior:
- Diagnostics export:
- Cache maintenance:

## Known Notes
- This script only writes local artifacts under $OutputRoot.
- It does not upload, push, tag, or sign releases.
"@ | Set-Content -Path $releaseNotesPath -Encoding UTF8

Write-Host "Publish complete"
Write-Host "Publish folder: $publishDir"
Write-Host "Zip: $zipPath"
Write-Host "SHA256: $checksumPath"
Write-Host "Release notes template: $releaseNotesPath"
