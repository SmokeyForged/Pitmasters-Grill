Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ExpectedBranch = 'feature/pmg-18-external-navigation-version'
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:GITHUB_EVENT_NAME -ne 'pull_request' -or $env:GITHUB_HEAD_REF -ne $ExpectedBranch) {
    throw "PMG-28 cutover carrier refuses to run outside the expected PR branch. event='$env:GITHUB_EVENT_NAME' head='$env:GITHUB_HEAD_REF'"
}

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $RepoRoot

git fetch origin $ExpectedBranch
git checkout -B $ExpectedBranch "origin/$ExpectedBranch"
if ($LASTEXITCODE -ne 0) { throw 'Failed to checkout exact PMG feature branch.' }

function Read-PreservedText([string]$Path) {
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    $hasBom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $text = [System.Text.Encoding]::UTF8.GetString($bytes)
    if ($text.Length -gt 0 -and $text[0] -eq [char]0xFEFF) {
        $text = $text.Substring(1)
    }
    $newline = if ($text.Contains("`r`n")) { "`r`n" } else { "`n" }
    [pscustomobject]@{
        Text = $text.Replace("`r`n", "`n")
        Newline = $newline
        HasBom = $hasBom
    }
}

function Write-PreservedText([string]$Path, [string]$Text, [string]$Newline, [bool]$HasBom) {
    $normalized = $Text.Replace("`n", $Newline)
    $encoding = [System.Text.UTF8Encoding]::new($HasBom)
    [System.IO.File]::WriteAllText($Path, $normalized, $encoding)
}

$xamlPath = 'PitmastersGrill/MainWindow.xaml'
$ctorPath = 'PitmastersGrill/MainWindow.ComposedConstructor.cs'
$mainPath = 'PitmastersGrill/MainWindow.xaml.cs'

$xamlFile = Read-PreservedText $xamlPath
$ctorFile = Read-PreservedText $ctorPath
$mainFile = Read-PreservedText $mainPath
$xaml = $xamlFile.Text
$ctor = $ctorFile.Text
$main = $mainFile.Text

$alreadyApplied = $xaml.Contains('x:Name="VersionUpdateViewControl"') -and
    $ctor.Contains('VersionUpdateViewControl.SetManualUpdateCheckEnabled') -and
    -not $main.Contains('ManualUpdateCheckButton_Click') -and
    -not $main.Contains('GitHubRepoLink_RequestNavigate')
if ($alreadyApplied) {
    Write-Host 'PMG-28 composition cutover already applied; no-op.'
    exit 0
}

$xamlPattern = '                            <TabItem Header="Version" Style="\{StaticResource PmgNestedSubTabItemStyle\}" Background="#1E252C" Foreground="#F2EFE6">\n.*?                            </TabItem>\n\n                            <TabItem Header="Diagnostics"'
$xamlMatches = [regex]::Matches($xaml, $xamlPattern, [System.Text.RegularExpressions.RegexOptions]::Singleline)
if ($xamlMatches.Count -ne 1) {
    throw "Expected exactly one inline Version tab block before cutover; found $($xamlMatches.Count)."
}
$xamlReplacement = @'
                            <TabItem Header="Version" Style="{StaticResource PmgNestedSubTabItemStyle}" Background="#1E252C" Foreground="#F2EFE6">
                                <views:VersionUpdateView x:Name="VersionUpdateViewControl"
                                                         RepositoryNavigateRequested="VersionUpdateView_RepositoryNavigateRequested"
                                                         ManualUpdateCheckRequested="VersionUpdateView_ManualUpdateCheckRequested"
                                                         AutomationProperties.AutomationId="VersionUpdateViewControl"/>
                            </TabItem>

                            <TabItem Header="Diagnostics"
'@
$xamlReplacement = $xamlReplacement.TrimStart("`r", "`n").Replace("`r`n", "`n")

$ctorOld = @'
            _manualUpdateCheckController = new ManualUpdateCheckController(
                this,
                ManualUpdateCheckButton,
                ManualUpdateStatusText,
                _browserLauncher,
                _appSettings,
                _windowShutdownCts.Token,
                () => _isShuttingDown);
'@.Replace("`r`n", "`n").TrimEnd("`r", "`n")
$ctorNew = @'
            _manualUpdateCheckController = new ManualUpdateCheckController(
                this,
                VersionUpdateViewControl.SetManualUpdateCheckEnabled,
                VersionUpdateViewControl.SetManualUpdateStatusText,
                OpenManualUpdateReleasePage,
                _appSettings,
                _windowShutdownCts.Token,
                () => _isShuttingDown);
'@.Replace("`r`n", "`n").TrimEnd("`r", "`n")

$manualOld = @'

        private async void ManualUpdateCheckButton_Click(object sender, RoutedEventArgs e)
        {
            if (_manualUpdateCheckController == null)
            {
                return;
            }

            await _manualUpdateCheckController.RunAsync();
        }
'@.Replace("`r`n", "`n").TrimEnd("`r", "`n")
$githubOld = @'

        private void GitHubRepoLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            var url = e.Uri?.AbsoluteUri ?? "https://github.com/SmokeyForged/Pitmasters-Grill";
            var result = ExternalNavigation.OpenUrl(url, "GitHub repository");
            ShowExternalNavigationErrorIfNeeded(result);
            e.Handled = true;
        }
'@.Replace("`r`n", "`n").TrimEnd("`r", "`n")

if (($ctor.Split($ctorOld).Count - 1) -ne 1) { throw 'Expected exactly one legacy ManualUpdateCheckController composition block.' }
if (($main.Split($manualOld).Count - 1) -ne 1) { throw 'Expected exactly one legacy ManualUpdateCheckButton_Click method.' }
if (($main.Split($githubOld).Count - 1) -ne 1) { throw 'Expected exactly one legacy GitHubRepoLink_RequestNavigate method.' }

$xaml = [regex]::Replace($xaml, $xamlPattern, [System.Text.RegularExpressions.MatchEvaluator]{ param($m) $xamlReplacement }, [System.Text.RegularExpressions.RegexOptions]::Singleline)
$ctor = $ctor.Replace($ctorOld, $ctorNew)
$main = $main.Replace($manualOld, '').Replace($githubOld, '')

Write-PreservedText $xamlPath $xaml $xamlFile.Newline $xamlFile.HasBom
Write-PreservedText $ctorPath $ctor $ctorFile.Newline $ctorFile.HasBom
Write-PreservedText $mainPath $main $mainFile.Newline $mainFile.HasBom

$actual = @(git diff --name-only | Sort-Object)
$expected = @(
    'PitmastersGrill/MainWindow.ComposedConstructor.cs',
    'PitmastersGrill/MainWindow.xaml',
    'PitmastersGrill/MainWindow.xaml.cs'
) | Sort-Object
$unexpected = Compare-Object $expected $actual
if ($unexpected) {
    throw "PMG-28 cutover changed an unexpected file set: $($actual -join ', ')"
}

git --no-pager diff --check
if ($LASTEXITCODE -ne 0) { throw 'git diff --check failed for PMG-28 cutover.' }

git config user.name 'Smokey Labs'
git config user.email 'greg.mcever@live.com'
git add -- $xamlPath $ctorPath $mainPath
git commit -m 'PMG-95 PMG-96 compose Version Updates view'
if ($LASTEXITCODE -ne 0) { throw 'Failed to create PMG-28 cutover commit.' }
git push origin "HEAD:$ExpectedBranch"
if ($LASTEXITCODE -ne 0) { throw 'Failed to push PMG-28 cutover commit to feature branch.' }
