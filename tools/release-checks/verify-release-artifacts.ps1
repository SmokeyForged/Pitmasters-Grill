[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ReleaseDirectory,

    [string]$ExpectedExeName = "PitmastersGrill.exe",

    [string]$ChecksumFileName = "SHA256SUMS.txt"
)

$ErrorActionPreference = "Stop"

Write-Host "=== PMG release artifact verification ==="

if (-not (Test-Path $ReleaseDirectory)) {
    throw "Release directory not found: $ReleaseDirectory"
}

$releasePath = Resolve-Path $ReleaseDirectory
$zipFiles = Get-ChildItem -Path $releasePath -Filter "*.zip" -File | Sort-Object Name

if ($zipFiles.Count -eq 0) {
    throw "No ZIP artifacts found in: $releasePath"
}

Add-Type -AssemblyName System.IO.Compression.FileSystem

$checksumLines = New-Object System.Collections.Generic.List[string]
$hadFailure = $false

foreach ($zip in $zipFiles) {
    Write-Host ""
    Write-Host "Verifying $($zip.Name)"
    Write-Host "----------------------------------------"

    try {
        $archive = [System.IO.Compression.ZipFile]::OpenRead($zip.FullName)

        try {
            $entries = @($archive.Entries)
            $exeEntry = $entries | Where-Object {
                [System.IO.Path]::GetFileName($_.FullName) -ieq $ExpectedExeName
            } | Select-Object -First 1

            if (-not $exeEntry) {
                Write-Error "Expected executable was not found in ZIP: $ExpectedExeName"
                $hadFailure = $true
            }
            else {
                Write-Host "Found executable: $($exeEntry.FullName)"
            }

            Write-Host "ZIP entries: $($entries.Count)"
        }
        finally {
            $archive.Dispose()
        }
    }
    catch {
        Write-Error "ZIP validation failed for $($zip.Name): $_"
        $hadFailure = $true
    }

    $hash = Get-FileHash -Algorithm SHA256 -Path $zip.FullName
    $line = "$($hash.Hash.ToLowerInvariant())  $($zip.Name)"
    $checksumLines.Add($line)
    Write-Host "SHA256: $($hash.Hash)"
}

$checksumPath = Join-Path $releasePath $ChecksumFileName
$checksumLines | Set-Content -Path $checksumPath -Encoding UTF8

Write-Host ""
Write-Host "Wrote checksum file: $checksumPath"

if ($hadFailure) {
    Write-Error "One or more release artifact checks failed."
    exit 1
}

Write-Host "Release artifact verification completed successfully."
exit 0
