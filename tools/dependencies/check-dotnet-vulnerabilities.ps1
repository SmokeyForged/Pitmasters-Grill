[CmdletBinding()]
param(
    [string[]]$Projects = @(
        "PitmastersGrill\PitmastersGrill.csproj",
        "PitmastersGrill.Tests\PitmastersGrill.Tests.csproj"
    )
)

$ErrorActionPreference = "Stop"

Write-Host "=== PMG dependency vulnerability check ==="

$foundVulnerability = $false

foreach ($project in $Projects) {
    if (-not (Test-Path $project)) {
        throw "Project file not found: $project"
    }

    Write-Host ""
    Write-Host "Checking $project"
    Write-Host "----------------------------------------"

    $output = & dotnet list $project package --vulnerable --include-transitive 2>&1
    $exitCode = $LASTEXITCODE

    $output | ForEach-Object { Write-Host $_ }

    if ($exitCode -ne 0) {
        throw "dotnet list package failed for $project with exit code $exitCode"
    }

    $joinedOutput = ($output | Out-String)

    if ($joinedOutput -match "has the following vulnerable packages") {
        $foundVulnerability = $true
    }
}

Write-Host ""

if ($foundVulnerability) {
    Write-Error "One or more vulnerable package reports were found."
    exit 1
}

Write-Host "No vulnerable package reports found."
exit 0
