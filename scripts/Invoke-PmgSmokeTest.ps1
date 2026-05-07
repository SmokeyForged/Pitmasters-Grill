<#

.SYNOPSIS

Runs PMG local regression/smoke validation checks.



.DESCRIPTION

This script automates PMG validation layers that are feasible from a local Windows development machine:

- repo/working-tree visibility

- explicit dotnet test/build paths

- git diff whitespace check

- MainWindow line-count reporting

- WPF process launch

- responsiveness/no-hang sampling

- normal close attempt

- optional UI Automation regression smoke

- recent PMG log discovery and failure tail capture



The optional -FullUiSmoke mode exercises visible UI controls through Windows UI Automation. It is intended

as a practical replacement for repeated manual smoke checks, while still avoiding destructive operations.



.PARAMETER RepoRoot

Repository root. Defaults to the parent folder of this script's directory.



.PARAMETER ResponsivenessSeconds

How long to sample the running WPF process for responsiveness.



.PARAMETER SampleIntervalSeconds

Seconds between responsiveness samples.



.PARAMETER RequireClean

Fail if the Git working tree has changes.



.PARAMETER EnforceLineGate

Fail if MainWindow.xaml.cs or MainWindow.xaml exceeds the configured line thresholds.

By default line counts are reported but not enforced because v1.4.0 refactor work may be in progress.



.PARAMETER FullUiSmoke

Runs the deeper UI Automation smoke pass after startup. This navigates tabs, toggles reversible settings,

exercises safe refresh buttons, and invokes confirmable maintenance buttons only far enough to cancel/no the dialog.



.PARAMETER ExerciseConfirmableActions

Allows -FullUiSmoke to click confirmation-based maintenance buttons and cancel/No the resulting dialog.

Without this flag, those buttons are only checked for presence/enabled state.



.PARAMETER UiInventory

Writes a UI Automation inventory to the transcript and skips no checks by itself. Useful for adding stable selectors.



.PARAMETER KeepAppOpen

Leaves the app running at the end of validation for manual inspection.



.PARAMETER SkipDotNet

Skip dotnet test/build.



.PARAMETER SkipDiffCheck

Skip git diff --check.



.PARAMETER SkipLaunch

Skip WPF launch/responsiveness/close checks.



.EXAMPLE

.\scripts\Invoke-PmgSmokeTest.ps1



.EXAMPLE

.\scripts\Invoke-PmgSmokeTest.ps1 -FullUiSmoke -ResponsivenessSeconds 30



.EXAMPLE

.\scripts\Invoke-PmgSmokeTest.ps1 -FullUiSmoke -ExerciseConfirmableActions

#>



[CmdletBinding()]

param(

    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,

    [int]$ResponsivenessSeconds = 60,

    [int]$SampleIntervalSeconds = 5,

    [switch]$RequireClean,

    [switch]$EnforceLineGate,

    [int]$MainWindowCodeBehindMaxLines = 2000,

    [int]$MainWindowXamlMaxLines = 2000,

    [switch]$FullUiSmoke,

    [switch]$ExerciseConfirmableActions,

    [switch]$UiInventory,

    [switch]$KeepAppOpen,
    [switch]$SkipBoardPopulationSmoke,
    [string]$BoardFixturePath = "",
    [int]$BoardPopulationTimeoutSeconds = 240,

    [switch]$SkipDotNet,

    [switch]$SkipDiffCheck,

    [switch]$SkipLaunch

)



Set-StrictMode -Version Latest

$ErrorActionPreference = "Stop"



$script:Failures = New-Object System.Collections.Generic.List[string]

$script:Warnings = New-Object System.Collections.Generic.List[string]

$script:UiSmokeFindings = New-Object System.Collections.Generic.List[string]



function Write-Section {

    param([Parameter(Mandatory)][string]$Title)

    Write-Host ""

    Write-Host "== $Title =="

}



function Add-Failure {

    param([Parameter(Mandatory)][string]$Message)

    $script:Failures.Add($Message) | Out-Null

    Write-Host "[FAIL] $Message" -ForegroundColor Red

}



function Add-Warning {

    param([Parameter(Mandatory)][string]$Message)

    $script:Warnings.Add($Message) | Out-Null

    Write-Host "[WARN] $Message" -ForegroundColor Yellow

}



function Add-Pass {

    param([Parameter(Mandatory)][string]$Message)

    Write-Host "[PASS] $Message" -ForegroundColor Green

}



function Add-UiFinding {

    param([Parameter(Mandatory)][string]$Message)

    $script:UiSmokeFindings.Add($Message) | Out-Null

    Write-Host "[UI] $Message"

}



function Assert-Path {

    param(

        [Parameter(Mandatory)][string]$Path,

        [Parameter(Mandatory)][string]$Description

    )



    if (-not (Test-Path -LiteralPath $Path)) {

        throw "$Description not found: $Path"

    }

}



function Invoke-NativeChecked {

    param(

        [Parameter(Mandatory)][string]$FilePath,

        [Parameter(Mandatory)][string[]]$Arguments,

        [Parameter(Mandatory)][string]$Description,

        [string]$WorkingDirectory = $RepoRoot

    )



    Write-Host ""

    Write-Host "> $FilePath $($Arguments -join ' ')"



    Push-Location $WorkingDirectory

    try {

        & $FilePath @Arguments

        $exitCode = $LASTEXITCODE

    }

    finally {

        Pop-Location

    }



    if ($exitCode -ne 0) {

        Add-Failure "$Description failed with exit code $exitCode."

        return $false

    }



    Add-Pass "$Description completed successfully."

    return $true

}



function Get-FileLineCount {

    param([Parameter(Mandatory)][string]$Path)

    return (Get-Content -LiteralPath $Path).Count

}



function Get-RecentPmgLogs {

    $logRoots = @(

        (Join-Path $env:LOCALAPPDATA "PitmastersGrill"),

        (Join-Path $env:APPDATA "PitmastersGrill"),

        $RepoRoot

    ) | Where-Object { $_ -and (Test-Path -LiteralPath $_) }



    $cutoff = (Get-Date).AddHours(-4)

    $found = foreach ($root in $logRoots) {

        Get-ChildItem -LiteralPath $root -Recurse -File -ErrorAction SilentlyContinue |

            Where-Object {

                $_.LastWriteTime -ge $cutoff -and

                ($_.Name -match 'log|trace|diagnostic')

            }

    }



    return $found | Sort-Object LastWriteTime -Descending

}



function Show-RecentLogs {

    param([int]$Tail = 80)



    Write-Section "Recent PMG logs"

    $logs = @(Get-RecentPmgLogs | Select-Object -First 10)



    if ($logs.Count -eq 0) {

        Add-Warning "No recent PMG log/trace/diagnostic files found in the expected locations."

        return

    }



    foreach ($log in $logs) {

        Write-Host ("{0:u} | {1,8} bytes | {2}" -f $log.LastWriteTime, $log.Length, $log.FullName)

    }



    if ($script:Failures.Count -gt 0) {

        $latest = $logs[0]

        Write-Host ""

        Write-Host "--- Tail of newest log: $($latest.FullName) ---"

        try {

            Get-Content -LiteralPath $latest.FullName -Tail $Tail

        }

        catch {

            Add-Warning "Failed to tail newest log: $($_.Exception.Message)"

        }

    }

}



function Initialize-UiAutomation {

    try {

        Add-Type -AssemblyName UIAutomationClient -ErrorAction Stop

        Add-Type -AssemblyName UIAutomationTypes -ErrorAction Stop

        Add-Type -AssemblyName System.Windows.Forms -ErrorAction Stop

        return $true

    }

    catch {

        Add-Failure "Failed to load UI Automation assemblies: $($_.Exception.Message)"

        return $false

    }

}



function Get-MainAutomationWindow {

    param(

        [Parameter(Mandatory)][System.Diagnostics.Process]$Process,

        [int]$TimeoutSeconds = 20

    )



    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)



    while ((Get-Date) -lt $deadline) {

        $Process.Refresh()

        if ($Process.HasExited) {

            return $null

        }



        $handle = $Process.MainWindowHandle

        if ($handle -ne [IntPtr]::Zero) {

            try {

                return [System.Windows.Automation.AutomationElement]::FromHandle($handle)

            }

            catch {

                Start-Sleep -Milliseconds 300

            }

        }



        Start-Sleep -Milliseconds 300

    }



    return $null

}



function Get-ControlTypeName {

    param([Parameter(Mandatory)]$ControlType)

    if ($null -eq $ControlType) { return "" }

    return $ControlType.ProgrammaticName -replace '^ControlType\.', ''

}



function Get-ElementSummary {

    param([Parameter(Mandatory)][System.Windows.Automation.AutomationElement]$Element)

    $name = $Element.Current.Name

    $automationId = $Element.Current.AutomationId

    $type = Get-ControlTypeName $Element.Current.ControlType

    return "Type='$type' Name='$name' AutomationId='$automationId' Enabled=$($Element.Current.IsEnabled)"

}



function Find-ElementsByControlType {

    param(

        [Parameter(Mandatory)][System.Windows.Automation.AutomationElement]$Root,

        [Parameter(Mandatory)]$ControlType

    )



    $condition = [System.Windows.Automation.PropertyCondition]::new(

        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,

        $ControlType)



    return @($Root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condition))

}



function Find-ElementByNames {

    param(

        [Parameter(Mandatory)][System.Windows.Automation.AutomationElement]$Root,

        [Parameter(Mandatory)][string[]]$Names,

        [object[]]$ControlTypes = @()

    )



    $candidates = New-Object System.Collections.Generic.List[System.Windows.Automation.AutomationElement]



    if ($ControlTypes.Count -gt 0) {

        foreach ($controlType in $ControlTypes) {

            foreach ($element in (Find-ElementsByControlType -Root $Root -ControlType $controlType)) {

                $candidates.Add($element) | Out-Null

            }

        }

    }

    else {

        $trueCondition = [System.Windows.Automation.Condition]::TrueCondition

        foreach ($element in @($Root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $trueCondition))) {

            $candidates.Add($element) | Out-Null

        }

    }



    foreach ($name in $Names) {

        foreach ($element in $candidates) {

            if ($element.Current.Name -eq $name -or $element.Current.AutomationId -eq $name) {

                return $element

            }

        }

    }



    foreach ($name in $Names) {

        foreach ($element in $candidates) {

            if (($element.Current.Name -like "*$name*") -or ($element.Current.AutomationId -like "*$name*")) {

                return $element

            }

        }

    }



    return $null

}



function Get-PatternOrNull {

    param(

        [Parameter(Mandatory)][System.Windows.Automation.AutomationElement]$Element,

        [Parameter(Mandatory)]$PatternId

    )



    $pattern = $null

    if ($Element.TryGetCurrentPattern($PatternId, [ref]$pattern)) {

        return $pattern

    }



    return $null

}



function Invoke-Element {

    param(

        [Parameter(Mandatory)][System.Windows.Automation.AutomationElement]$Element,

        [Parameter(Mandatory)][string]$Description

    )



    $invoke = Get-PatternOrNull -Element $Element -PatternId ([System.Windows.Automation.InvokePattern]::Pattern)

    if ($null -ne $invoke) {

        $invoke.Invoke()

        Add-Pass "Invoked $Description."

        Start-Sleep -Milliseconds 600

        return $true

    }



    $selection = Get-PatternOrNull -Element $Element -PatternId ([System.Windows.Automation.SelectionItemPattern]::Pattern)

    if ($null -ne $selection) {

        $selection.Select()

        Add-Pass "Selected $Description."

        Start-Sleep -Milliseconds 600

        return $true

    }



    Add-Failure "Element does not support Invoke or SelectionItem pattern: $Description; $(Get-ElementSummary $Element)"

    return $false

}




function Try-Select-TabByName {
    param(
        [Parameter(Mandatory)][System.Windows.Automation.AutomationElement]$Root,
        [Parameter(Mandatory)][string[]]$Names,
        [Parameter(Mandatory)][string]$Description
    )

    $tab = Find-ElementByNames -Root $Root -Names $Names -ControlTypes @([System.Windows.Automation.ControlType]::TabItem)
    if ($null -eq $tab) {
        return $false
    }

    return Invoke-Element -Element $tab -Description "tab '$($tab.Current.Name)' for $Description"
}


function Select-SettingsSubTab {
    param(
        [Parameter(Mandatory)][System.Windows.Automation.AutomationElement]$Root,
        [Parameter(Mandatory)][string[]]$Names,
        [Parameter(Mandatory)][string]$Description
    )

    Select-TabByName -Root $Root -Names @("Settings", "SettingsTab") -Description "Settings tab before $Description" | Out-Null
    Start-Sleep -Milliseconds 400

    if (Try-Select-TabByName -Root $Root -Names $Names -Description $Description) {
        Start-Sleep -Milliseconds 400
        return $true
    }

    Add-Warning "Could not select Settings subtab for ${Description}. Names tried: $($Names -join ', ')"
    return $false
}

function Select-DiagnosticsArea {
    param(
        [Parameter(Mandatory)][System.Windows.Automation.AutomationElement]$Root,
        [Parameter(Mandatory)][string]$Description
    )

    $diagnosticsNames = @(
        "DiagnosticsSupportTab",
        "Diagnostics",
        "Diagnostics & Support",
        "Diagnostics/Support",
        "Logs & Diagnostics",
        "Support",
        "Logs",
        "Troubleshooting"
    )

    if (Try-Select-TabByName -Root $Root -Names $diagnosticsNames -Description $Description) {
        return $true
    }

    # Current PMG places Diagnostics under the Settings tab; older/alternate layouts may place it under Help.
    $parentTabCandidates = @(
        @{ Names = @("Settings", "SettingsTab"); Description = "Settings tab before $Description" },
        @{ Names = @("Help", "HelpTab"); Description = "Help tab before $Description" }
    )

    foreach ($parent in $parentTabCandidates) {
        if (Try-Select-TabByName -Root $Root -Names $parent.Names -Description $parent.Description) {
            Start-Sleep -Milliseconds 500

            if (Try-Select-TabByName -Root $Root -Names $diagnosticsNames -Description $Description) {
                return $true
            }
        }
    }

    Add-Failure "Could not find Diagnostics/Support area for ${Description}. Tried top-level, Settings-nested, and Help-nested diagnostics names: $($diagnosticsNames -join ', ')"
    return $false
}

function Select-TabByName {

    param(

        [Parameter(Mandatory)][System.Windows.Automation.AutomationElement]$Root,

        [Parameter(Mandatory)][string[]]$Names,

        [Parameter(Mandatory)][string]$Description

    )



    $tab = Find-ElementByNames -Root $Root -Names $Names -ControlTypes @([System.Windows.Automation.ControlType]::TabItem)

    if ($null -eq $tab) {

        Add-Failure "Could not find tab for $Description. Names tried: $($Names -join ', ')"

        return $false

    }



    return Invoke-Element -Element $tab -Description "tab '$($tab.Current.Name)' for $Description"

}



function Get-AppDialogWindows {

    param(

        [Parameter(Mandatory)][System.Diagnostics.Process]$Process,

        [Parameter(Mandatory)][System.Windows.Automation.AutomationElement]$MainWindow

    )



    $processCondition = [System.Windows.Automation.PropertyCondition]::new(

        [System.Windows.Automation.AutomationElement]::ProcessIdProperty,

        $Process.Id)

    $windowCondition = [System.Windows.Automation.PropertyCondition]::new(

        [System.Windows.Automation.AutomationElement]::ControlTypeProperty,

        [System.Windows.Automation.ControlType]::Window)

    $condition = [System.Windows.Automation.AndCondition]::new($processCondition, $windowCondition)



    $windows = @([System.Windows.Automation.AutomationElement]::RootElement.FindAll(

        [System.Windows.Automation.TreeScope]::Children,

        $condition))



    return $windows | Where-Object { $_.Current.NativeWindowHandle -ne $MainWindow.Current.NativeWindowHandle }

}



function Dismiss-AppDialogs {

    param(

        [Parameter(Mandatory)][System.Diagnostics.Process]$Process,

        [Parameter(Mandatory)][System.Windows.Automation.AutomationElement]$MainWindow,

        [string[]]$PreferredButtons = @("No", "Cancel", "OK", "Close")

    )



    $dismissed = 0



    for ($attempt = 1; $attempt -le 8; $attempt++) {

        Start-Sleep -Milliseconds 300

        $dialogs = @(Get-AppDialogWindows -Process $Process -MainWindow $MainWindow)



        if ($dialogs.Count -eq 0) {

            break

        }



        foreach ($dialog in $dialogs) {

            Write-Host "Dialog detected: $(Get-ElementSummary $dialog)"

            $button = Find-ElementByNames -Root $dialog -Names $PreferredButtons -ControlTypes @([System.Windows.Automation.ControlType]::Button)



            if ($null -ne $button) {

                Invoke-Element -Element $button -Description "dialog button '$($button.Current.Name)'" | Out-Null

                $dismissed++

            }

            else {

                Add-Warning "Dialog appeared but no preferred dismiss button was found. Sending Escape. Dialog: $(Get-ElementSummary $dialog)"

                [System.Windows.Forms.SendKeys]::SendWait("{ESC}")

                $dismissed++

            }

        }

    }



    return $dismissed

}



function Invoke-ButtonByNames {

    param(

        [Parameter(Mandatory)][System.Windows.Automation.AutomationElement]$Root,

        [Parameter(Mandatory)][string[]]$Names,

        [Parameter(Mandatory)][string]$Description,

        [switch]$Required

    )



    $button = Find-ElementByNames -Root $Root -Names $Names -ControlTypes @([System.Windows.Automation.ControlType]::Button)

    if ($null -eq $button) {

        $message = "Button not found for $Description. Names tried: $($Names -join ', ')"

        if ($Required) { Add-Failure $message } else { Add-Warning $message }

        return $false

    }



    if (-not $button.Current.IsEnabled) {

        Add-Warning "Button is present but disabled for ${Description}: $(Get-ElementSummary $button)"

        return $false

    }



    return Invoke-Element -Element $button -Description "button '$($button.Current.Name)' for $Description"

}



function Assert-ButtonPresent {

    param(

        [Parameter(Mandatory)][System.Windows.Automation.AutomationElement]$Root,

        [Parameter(Mandatory)][string[]]$Names,

        [Parameter(Mandatory)][string]$Description

    )



    $button = Find-ElementByNames -Root $Root -Names $Names -ControlTypes @([System.Windows.Automation.ControlType]::Button)

    if ($null -eq $button) {

        Add-Failure "Expected button not found for $Description. Names tried: $($Names -join ', ')"

        return $false

    }



    Add-Pass "Found button for ${Description}: $(Get-ElementSummary $button)"

    return $true

}



function Toggle-CheckBoxAndRestore {

    param(

        [Parameter(Mandatory)][System.Windows.Automation.AutomationElement]$Root,

        [Parameter(Mandatory)][string[]]$Names,

        [Parameter(Mandatory)][string]$Description,

        [switch]$Required

    )



    $box = Find-ElementByNames -Root $Root -Names $Names -ControlTypes @([System.Windows.Automation.ControlType]::CheckBox)

    if ($null -eq $box) {

        $message = "Checkbox not found for $Description. Names tried: $($Names -join ', ')"

        if ($Required) { Add-Failure $message } else { Add-Warning $message }

        return

    }



    $toggle = Get-PatternOrNull -Element $box -PatternId ([System.Windows.Automation.TogglePattern]::Pattern)

    if ($null -eq $toggle) {

        Add-Failure "Checkbox does not support TogglePattern for ${Description}: $(Get-ElementSummary $box)"

        return

    }



    $original = $toggle.Current.ToggleState

    Add-UiFinding "$Description original state: $original"



    $toggle.Toggle()

    Start-Sleep -Milliseconds 800

    $box = Find-ElementByNames -Root $Root -Names $Names -ControlTypes @([System.Windows.Automation.ControlType]::CheckBox)

    $afterFirst = (Get-PatternOrNull -Element $box -PatternId ([System.Windows.Automation.TogglePattern]::Pattern)).Current.ToggleState

    Add-UiFinding "$Description after toggle: $afterFirst"



    $toggle = Get-PatternOrNull -Element $box -PatternId ([System.Windows.Automation.TogglePattern]::Pattern)

    $toggle.Toggle()

    Start-Sleep -Milliseconds 800

    $box = Find-ElementByNames -Root $Root -Names $Names -ControlTypes @([System.Windows.Automation.ControlType]::CheckBox)

    $final = (Get-PatternOrNull -Element $box -PatternId ([System.Windows.Automation.TogglePattern]::Pattern)).Current.ToggleState

    Add-UiFinding "$Description restored state: $final"



    if ($final -ne $original) {

        Add-Failure "$Description did not restore to original state. Original=$original Final=$final"

    }

    else {

        Add-Pass "$Description toggled and restored."

    }

}



function Adjust-SliderAndRestore {

    param(

        [Parameter(Mandatory)][System.Windows.Automation.AutomationElement]$Root,

        [Parameter(Mandatory)][string[]]$Names,

        [Parameter(Mandatory)][string]$Description,

        [double]$Delta = -5

    )



    $slider = Find-ElementByNames -Root $Root -Names $Names -ControlTypes @([System.Windows.Automation.ControlType]::Slider)

    if ($null -eq $slider) {

        Add-Warning "Slider not found for $Description. Names tried: $($Names -join ', ')"

        return

    }



    $range = Get-PatternOrNull -Element $slider -PatternId ([System.Windows.Automation.RangeValuePattern]::Pattern)

    if ($null -eq $range) {

        Add-Failure "Slider does not support RangeValuePattern for ${Description}: $(Get-ElementSummary $slider)"

        return

    }



    $original = [double]$range.Current.Value

    $minimum = [double]$range.Current.Minimum

    $maximum = [double]$range.Current.Maximum

    $target = $original + $Delta



    if ($target -lt $minimum) { $target = [Math]::Min($maximum, $original + [Math]::Abs($Delta)) }

    if ($target -gt $maximum) { $target = [Math]::Max($minimum, $original - [Math]::Abs($Delta)) }



    Add-UiFinding "$Description original value: $original; target value: $target"

    $range.SetValue($target)

    Start-Sleep -Milliseconds 800



    $slider = Find-ElementByNames -Root $Root -Names $Names -ControlTypes @([System.Windows.Automation.ControlType]::Slider)

    $range = Get-PatternOrNull -Element $slider -PatternId ([System.Windows.Automation.RangeValuePattern]::Pattern)

    $range.SetValue($original)

    Start-Sleep -Milliseconds 800



    Add-Pass "$Description adjusted and restored."

}



function Write-UiInventory {

    param([Parameter(Mandatory)][System.Windows.Automation.AutomationElement]$Root)



    Write-Section "UI Automation inventory"

    $types = @(

        [System.Windows.Automation.ControlType]::TabItem,

        [System.Windows.Automation.ControlType]::Button,

        [System.Windows.Automation.ControlType]::CheckBox,

        [System.Windows.Automation.ControlType]::ComboBox,

        [System.Windows.Automation.ControlType]::Slider,

        [System.Windows.Automation.ControlType]::Edit,

        [System.Windows.Automation.ControlType]::Text

    )



    foreach ($type in $types) {

        $elements = @(Find-ElementsByControlType -Root $Root -ControlType $type)

        Write-Host "-- $(Get-ControlTypeName $type): $($elements.Count) --"

        foreach ($element in ($elements | Select-Object -First 200)) {

            Write-Host (Get-ElementSummary $element)

        }

    }

}



function Invoke-FullUiSmoke {

    param(

        [Parameter(Mandatory)][System.Diagnostics.Process]$Process,

        [Parameter(Mandatory)][System.Windows.Automation.AutomationElement]$MainWindow

    )



    Write-Section "Full UI Automation smoke"



    if ($UiInventory) {

        Write-UiInventory -Root $MainWindow

    }



    # Top-level tab navigation. Names include fallbacks because visible labels may evolve.

    Select-TabByName -Root $MainWindow -Names @("Grill", "Pilot Board", "Board") -Description "Grill tab" | Out-Null

    Select-TabByName -Root $MainWindow -Names @("Analysis") -Description "Analysis tab" | Out-Null

    Select-TabByName -Root $MainWindow -Names @("Intel", "Public Data", "Intel/Public Data") -Description "Intel/Public Data tab" | Out-Null

    Select-TabByName -Root $MainWindow -Names @("Intel Status", "Status") -Description "Intel Status nested tab" | Out-Null

    Select-TabByName -Root $MainWindow -Names @("Config", "Intel Config") -Description "Intel Config nested tab" | Out-Null

    Select-DiagnosticsArea -Root $MainWindow -Description "Diagnostics tab" | Out-Null

    Select-TabByName -Root $MainWindow -Names @("Settings") -Description "Settings tab" | Out-Null

    Select-TabByName -Root $MainWindow -Names @("Help") -Description "Help tab" | Out-Null

    Select-TabByName -Root $MainWindow -Names @("Grill", "Pilot Board", "Board") -Description "return to Grill tab" | Out-Null





    if (-not $SkipBoardPopulationSmoke) {

        Test-BoardPopulationFromFixture -Root $MainWindow -FixturePath $effectiveBoardFixturePath -TimeoutSeconds $BoardPopulationTimeoutSeconds

    }

    else {

        Add-Warning "Skipping board fixture population because -SkipBoardPopulationSmoke was specified."

    }
    # Safe reversible settings checks. Missing names are warnings unless essential.
    Select-SettingsSubTab -Root $MainWindow -Names @(
        "Display",
        "Appearance",
        "General",
        "Preferences",
        "Settings"
    ) -Description "appearance/general settings subtab" | Out-Null

    Toggle-CheckBoxAndRestore -Root $MainWindow -Names @(
        "DarkModeCheckBox",
        "Dark Mode",
        "Dark mode"
    ) -Description "Dark mode setting"

    Toggle-CheckBoxAndRestore -Root $MainWindow -Names @(
        "AlwaysOnTopCheckBox",
        "Always on top",
        "Always On Top"
    ) -Description "Always-on-top setting"

    Adjust-SliderAndRestore -Root $MainWindow -Names @(
        "WindowOpacitySlider",
        "Window opacity",
        "Opacity"
    ) -Description "Window opacity setting"

    Select-SettingsSubTab -Root $MainWindow -Names @(
        "Board",
        "Board Display",
        "Grill Board",
        "Columns",
        "Layout",
        "Display"
    ) -Description "board display settings subtab" | Out-Null

    Toggle-CheckBoxAndRestore -Root $MainWindow -Names @(
        "ShowBoardGridLinesCheckBox",
        "Show grid lines",
        "Grid lines"
    ) -Description "Board grid lines setting"

    Select-TabByName -Root $MainWindow -Names @("Intel", "Public Data", "Intel/Public Data") -Description "Intel/Public Data tab for reversible Intel settings" | Out-Null

    Select-TabByName -Root $MainWindow -Names @("Config", "Intel Config") -Description "Intel Config nested tab" | Out-Null

    Toggle-CheckBoxAndRestore -Root $MainWindow -Names @("Enable live zKill feed", "Live zKill", "R2Z2", "EnableLiveZkillFeedCheckBox") -Description "live zKill/R2Z2 setting"

    Toggle-CheckBoxAndRestore -Root $MainWindow -Names @("Background historical repair", "Historical repair", "BackgroundHistoricalRepairEnabledCheckBox") -Description "background historical repair setting"



    # Safe refresh buttons.

    Select-DiagnosticsArea -Root $MainWindow -Description "Diagnostics tab for refresh buttons" | Out-Null

    Invoke-ButtonByNames -Root $MainWindow -Names @("RefreshProviderHealthButton", "Refresh Provider Health", "Provider Health") -Description "refresh provider health" | Out-Null

    Invoke-ButtonByNames -Root $MainWindow -Names @("RefreshCacheStatsButton", "Refresh Cache Stats", "Cache Stats") -Description "refresh cache stats" | Out-Null



    # Confirmable actions. By default verify presence only. With -ExerciseConfirmableActions, click and cancel/No.

    $confirmableActions = @(

        @{ Names = @("ClearExpiredCacheButton", "Clear Expired Cache", "Clear expired"); Description = "clear expired cache confirmation" },

        @{ Names = @("VacuumCacheButton", "Vacuum Cache", "Vacuum"); Description = "vacuum cache confirmation" },

        @{ Names = @("ClearAllCacheButton", "Clear All Cache", "Clear all"); Description = "clear all cache confirmation" },

        @{ Names = @("RebuildKillmailDerivedIntelButton", "Rebuild Killmail Derived Intel", "Rebuild Derived Intel", "Derived Intel"); Description = "derived intel rebuild confirmation" }

    )



    foreach ($action in $confirmableActions) {

        if ($ExerciseConfirmableActions) {

            $beforeDialogs = @(Get-AppDialogWindows -Process $Process -MainWindow $MainWindow).Count

            $clicked = Invoke-ButtonByNames -Root $MainWindow -Names $action.Names -Description $action.Description

            if ($clicked) {

                $dismissed = Dismiss-AppDialogs -Process $Process -MainWindow $MainWindow -PreferredButtons @("No", "Cancel", "OK", "Close")

                if ($dismissed -lt 1) {

                    Add-Warning "No dialog was detected after invoking $($action.Description). If this action is destructive without confirmation, add an app-level test hook before making this required."

                }

                else {

                    Add-Pass "Invoked and dismissed confirmation path for $($action.Description)."

                }

            }

        }

        else {

            Assert-ButtonPresent -Root $MainWindow -Names $action.Names -Description $action.Description | Out-Null

        }

    }



    # Ensure no dialogs remain open after smoke.

    $remaining = Dismiss-AppDialogs -Process $Process -MainWindow $MainWindow -PreferredButtons @("No", "Cancel", "OK", "Close")

    if ($remaining -gt 0) {

        Add-Warning "Dismissed $remaining leftover dialog(s) after UI smoke."

    }



    $Process.Refresh()

    if ($Process.HasExited) {

        Add-Failure "PMG exited during full UI smoke. ExitCode=$($Process.ExitCode)"

    }

    elseif (-not $Process.Responding) {

        Add-Failure "PMG is not responding after full UI smoke."

    }

    else {

        Add-Pass "Full UI Automation smoke completed with app still responsive."

    }

}




function Find-ElementByAutomationId {
    param(
        [Parameter(Mandatory)][System.Windows.Automation.AutomationElement]$Root,
        [Parameter(Mandatory)][string]$AutomationId
    )

    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty,
        $AutomationId
    )

    return $Root.FindFirst(
        [System.Windows.Automation.TreeScope]::Descendants,
        $condition
    )
}

function Get-ElementNameByAutomationId {
    param(
        [Parameter(Mandatory)][System.Windows.Automation.AutomationElement]$Root,
        [Parameter(Mandatory)][string]$AutomationId
    )

    $element = Find-ElementByAutomationId -Root $Root -AutomationId $AutomationId
    if ($null -eq $element) {
        return $null
    }

    try {
        return [string]$element.Current.Name
    }
    catch {
        return $null
    }
}

function Get-VisibleCountFromBoardSummary {
    param([AllowNull()][string]$SummaryText)

    if ([string]::IsNullOrWhiteSpace($SummaryText)) {
        return $null
    }

    if ($SummaryText -match 'Visible\s+(\d+)') {
        return [int]$Matches[1]
    }

    return $null
}

function Test-BoardPopulationFromFixture {
    param(
        [Parameter(Mandatory)][System.Windows.Automation.AutomationElement]$Root,
        [Parameter(Mandatory)][string]$FixturePath,
        [Parameter(Mandatory)][int]$TimeoutSeconds
    )

    Write-Section "Board population fixture smoke"

    if (-not (Test-Path -LiteralPath $FixturePath)) {
        Add-Failure "Board population fixture was not found: $FixturePath"
        return
    }

    $fixtureLines = @(Get-Content -LiteralPath $FixturePath | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    $expectedCount = $fixtureLines.Count
    $fixtureText = Get-Content -LiteralPath $FixturePath -Raw

    if ($expectedCount -le 0 -or [string]::IsNullOrWhiteSpace($fixtureText)) {
        Add-Failure "Board population fixture is empty or unreadable: $FixturePath"
        return
    }

    Add-Pass "Loaded board fixture with $expectedCount non-empty line(s): $FixturePath"

    Select-TabByName -Root $Root -Names @("Grill") -Description "Grill tab before board population fixture smoke" | Out-Null
    Start-Sleep -Milliseconds 500

    $statusBefore = Get-ElementNameByAutomationId -Root $Root -AutomationId "BoardPopulationStatusText"
    $summaryBefore = Get-ElementNameByAutomationId -Root $Root -AutomationId "BoardSummaryText"

    Write-Host "Board status before fixture: $statusBefore"
    Write-Host "Board summary before fixture: $summaryBefore"

    try {
        $sentinelText = "PMG_SMOKE_TEST_CLIPBOARD_RESET_$([Guid]::NewGuid().ToString('N'))"
        Set-Clipboard -Value $sentinelText
        Start-Sleep -Milliseconds 750
        Set-Clipboard -Value $fixtureText
        Start-Sleep -Milliseconds 750
    }
    catch {
        Add-Failure "Failed to perform clipboard state-change sequence for board fixture text: $($_.Exception.Message)"
        return
    }

    Add-Pass "Changed clipboard state, then copied board fixture text for PMG clipboard-triggered board population."

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $sawStatusChange = $false
    $sawVisibleRows = $false
    $lastStatus = $statusBefore
    $lastSummary = $summaryBefore
    $lastVisible = Get-VisibleCountFromBoardSummary -SummaryText $summaryBefore
    $requiredCompleteStatus = "Board population complete"
    $sawExpectedVisibleRows = $false

    while ((Get-Date) -lt $deadline) {
        Start-Sleep -Seconds 2

        $status = Get-ElementNameByAutomationId -Root $Root -AutomationId "BoardPopulationStatusText"
        $summary = Get-ElementNameByAutomationId -Root $Root -AutomationId "BoardSummaryText"
        $visible = Get-VisibleCountFromBoardSummary -SummaryText $summary
        $statusNormalized = ""
        if ($null -ne $status) {
            $statusNormalized = $status.Trim()
        }

        if ($null -ne $status -and $status -ne $statusBefore) {
            $sawStatusChange = $true
        }

        if ($null -ne $visible -and $visible -gt 0) {
            $sawVisibleRows = $true
        }

        if ($null -ne $visible -and $visible -ge $expectedCount) {
            $sawExpectedVisibleRows = $true
        }

        if ($status -ne $lastStatus -or $summary -ne $lastSummary) {
            Write-Host "Board status: $status"
            Write-Host "Board summary: $summary"
        }

        $lastStatus = $status
        $lastSummary = $summary
        $lastVisible = $visible

        if ($sawExpectedVisibleRows -and $statusNormalized -eq $requiredCompleteStatus) {
            Add-Pass "Board fixture population completed. Visible=$visible Summary='$summary' Status='$status'"
            return
        }

        if ($statusNormalized -eq $requiredCompleteStatus -and -not $sawExpectedVisibleRows) {
            Add-Failure "Board status reached '$requiredCompleteStatus' before visible row count reached $expectedCount. LastVisible=$lastVisible LastSummary='$lastSummary'"
            return
        }
    }

    Add-Failure "Timed out after $TimeoutSeconds seconds waiting for board fixture population to complete. RequiredStatus='$requiredCompleteStatus' SawStatusChange=$sawStatusChange SawVisibleRows=$sawVisibleRows SawExpectedVisibleRows=$sawExpectedVisibleRows LastVisible=$lastVisible LastStatus='$lastStatus' LastSummary='$lastSummary'"
}

function Test-PmgLaunch {

    param([Parameter(Mandatory)][string]$ExePath)



    Write-Section "Runtime launch/responsiveness smoke"



    $proc = $null



    try {

        Write-Host "Launching: $ExePath"

        $proc = Start-Process -FilePath $ExePath -WorkingDirectory (Split-Path -Parent $ExePath) -PassThru



        $deadline = (Get-Date).AddSeconds(25)

        $sawWindow = $false



        while ((Get-Date) -lt $deadline) {

            Start-Sleep -Milliseconds 500

            $proc.Refresh()



            if ($proc.HasExited) {

                Add-Failure "PMG exited during startup. ExitCode=$($proc.ExitCode)"

                return

            }



            if ($proc.MainWindowHandle -ne [IntPtr]::Zero) {

                $sawWindow = $true

                break

            }

        }



        if (-not $sawWindow) {

            Add-Failure "PMG process started but no main window handle appeared within 25 seconds."

            return

        }



        Add-Pass "PMG main window handle appeared."



        $sampleCount = [Math]::Max(1, [Math]::Ceiling($ResponsivenessSeconds / [Math]::Max(1, $SampleIntervalSeconds)))

        $allResponsive = $true



        for ($i = 1; $i -le $sampleCount; $i++) {

            Start-Sleep -Seconds $SampleIntervalSeconds

            $proc.Refresh()



            if ($proc.HasExited) {

                Add-Failure "PMG exited during responsiveness sampling. ExitCode=$($proc.ExitCode)"

                return

            }



            $responding = $false



            try {

                $responding = [bool]$proc.Responding

            }

            catch {

                Add-Warning "Could not read Process.Responding for sample ${i}: $($_.Exception.Message)"

                $responding = $false

            }



            Write-Host ("Sample {0}/{1}: Responding={2}; WindowTitle='{3}'" -f $i, $sampleCount, $responding, $proc.MainWindowTitle)



            if (-not $responding) {

                $allResponsive = $false

            }

        }



        if ($allResponsive) {

            Add-Pass "PMG remained responsive for approximately $ResponsivenessSeconds seconds."

        }

        else {

            Add-Failure "PMG reported non-responsive during at least one sample."

        }



        if ($FullUiSmoke -or $UiInventory) {

            if (Initialize-UiAutomation) {

                $mainAutomationWindow = Get-MainAutomationWindow -Process $proc -TimeoutSeconds 20

                if ($null -eq $mainAutomationWindow) {

                    Add-Failure "Could not resolve main AutomationElement for PMG window."

                }

                else {

                    if ($FullUiSmoke) {

                        Invoke-FullUiSmoke -Process $proc -MainWindow $mainAutomationWindow

                    }

                    elseif ($UiInventory) {

                        Write-UiInventory -Root $mainAutomationWindow

                    }

                }

            }

        }



        $proc.Refresh()



        if ($proc.HasExited) {

            Add-Pass "PMG exited before close request. ExitCode=$($proc.ExitCode)"

            return

        }



        if ($KeepAppOpen) {

            Add-Warning "Leaving PMG open because -KeepAppOpen was specified. Validation script will not close it."

            return

        }



        Write-Host "Requesting normal close via CloseMainWindow()."

        $closeRequested = $proc.CloseMainWindow()



        if (-not $closeRequested) {

            Add-Failure "CloseMainWindow() returned false."

            return

        }



        $closed = $proc.WaitForExit(15000)



        if (-not $closed) {

            Add-Failure "PMG did not close within 15 seconds after CloseMainWindow()."

            return

        }



        $proc.Refresh()

        Add-Pass "PMG closed normally. ExitCode=$($proc.ExitCode)"

    }

    finally {

        if ($null -ne $proc -and -not $KeepAppOpen) {

            try {

                $proc.Refresh()



                if (-not $proc.HasExited) {

                    Add-Warning "Killing leftover PMG validation process after failure."

                    Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue

                }

            }

            catch {

                Add-Warning "Failed during cleanup of PMG process: $($_.Exception.Message)"

            }

        }

    }

}



$RepoRoot = (Resolve-Path -LiteralPath $RepoRoot).Path

$artifactRoot = Join-Path $RepoRoot "artifacts\smoke-tests"

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"

$runDir = Join-Path $artifactRoot $timestamp

New-Item -ItemType Directory -Path $runDir -Force | Out-Null



$transcriptPath = Join-Path $runDir "pmg-smoke-test-transcript.txt"



try {

    Start-Transcript -Path $transcriptPath -Force | Out-Null

}

catch {

    Write-Host "[WARN] Failed to start transcript: $($_.Exception.Message)" -ForegroundColor Yellow

}



try {

    Write-Section "PMG smoke/regression validation"

    Write-Host "Repo root: $RepoRoot"

    Write-Host "Transcript: $transcriptPath"

    Write-Host "FullUiSmoke: $FullUiSmoke"
    $effectiveBoardFixturePath = $BoardFixturePath
    if ([string]::IsNullOrWhiteSpace($effectiveBoardFixturePath)) {
        $effectiveBoardFixturePath = Join-Path (Join-Path $RepoRoot "test-fixtures") "clipboard-large-local-list-valid.txt"
    }
    Write-Host "Board fixture path: $effectiveBoardFixturePath"
    Write-Host "SkipBoardPopulationSmoke: $SkipBoardPopulationSmoke"

    Write-Host "ExerciseConfirmableActions: $ExerciseConfirmableActions"



    $appProject = Join-Path $RepoRoot "PitmastersGrill\PitmastersGrill.csproj"

    $testProject = Join-Path $RepoRoot "PitmastersGrill.Tests\PitmastersGrill.Tests.csproj"

    $mainWindowCodeBehind = Join-Path $RepoRoot "PitmastersGrill\MainWindow.xaml.cs"

    $mainWindowXaml = Join-Path $RepoRoot "PitmastersGrill\MainWindow.xaml"

    $appExe = Join-Path $RepoRoot "PitmastersGrill\bin\Debug\net10.0-windows\PitmastersGrill.exe"



    Assert-Path $appProject "App project"

    Assert-Path $testProject "Test project"

    Assert-Path $mainWindowCodeBehind "MainWindow code-behind"

    Assert-Path $mainWindowXaml "MainWindow XAML"



    Write-Section "Git state"



    $branch = (& git -C $RepoRoot branch --show-current).Trim()

    Write-Host "Branch: $branch"



    $statusLines = @(& git -C $RepoRoot status --short)



    if ($statusLines.Count -eq 0) {

        Add-Pass "Working tree is clean."

    }

    else {

        Write-Host "Working tree status:"

        $statusLines | ForEach-Object { Write-Host $_ }



        if ($RequireClean) {

            Add-Failure "Working tree is not clean and -RequireClean was specified."

        }

        else {

            Add-Warning "Working tree is not clean. Continuing because -RequireClean was not specified."

        }

    }



    Write-Section "MainWindow line counts"



    $codeBehindLines = Get-FileLineCount $mainWindowCodeBehind

    $xamlLines = Get-FileLineCount $mainWindowXaml



    Write-Host "MainWindow.xaml.cs: $codeBehindLines"

    Write-Host "MainWindow.xaml:    $xamlLines"



    if ($EnforceLineGate) {

        if ($codeBehindLines -ge $MainWindowCodeBehindMaxLines) {

            Add-Failure "MainWindow.xaml.cs line count $codeBehindLines is not under $MainWindowCodeBehindMaxLines."

        }

        else {

            Add-Pass "MainWindow.xaml.cs is under $MainWindowCodeBehindMaxLines lines."

        }



        if ($xamlLines -ge $MainWindowXamlMaxLines) {

            Add-Failure "MainWindow.xaml line count $xamlLines is not under $MainWindowXamlMaxLines."

        }

        else {

            Add-Pass "MainWindow.xaml is under $MainWindowXamlMaxLines lines."

        }

    }

    else {

        if ($xamlLines -ge $MainWindowXamlMaxLines) {

            Add-Warning "MainWindow.xaml is not under $MainWindowXamlMaxLines lines."

        }

        else {

            Add-Pass "MainWindow.xaml is under $MainWindowXamlMaxLines lines."

        }



        if ($codeBehindLines -ge $MainWindowCodeBehindMaxLines) {

            Add-Warning "MainWindow.xaml.cs is not under $MainWindowCodeBehindMaxLines lines yet. This is reported but not failed without -EnforceLineGate."

        }

        else {

            Add-Pass "MainWindow.xaml.cs is under $MainWindowCodeBehindMaxLines lines."

        }

    }



    if (-not $SkipDotNet) {

        Write-Section "Build/test"

        Invoke-NativeChecked -FilePath "dotnet" -Arguments @("test", $testProject) -Description "dotnet test" | Out-Null

        Invoke-NativeChecked -FilePath "dotnet" -Arguments @("build", $appProject) -Description "dotnet build" | Out-Null

    }

    else {

        Add-Warning "Skipping dotnet test/build because -SkipDotNet was specified."

    }



    if (-not $SkipDiffCheck) {

        Write-Section "Diff whitespace check"

        Invoke-NativeChecked -FilePath "git" -Arguments @("-C", $RepoRoot, "--no-pager", "diff", "--check") -Description "git diff --check" | Out-Null

    }

    else {

        Add-Warning "Skipping git diff --check because -SkipDiffCheck was specified."

    }



    if (-not $SkipLaunch) {

        Assert-Path $appExe "Built app executable"

        Test-PmgLaunch -ExePath $appExe

    }

    else {

        Add-Warning "Skipping WPF launch/responsiveness checks because -SkipLaunch was specified."

    }



    Show-RecentLogs



    Write-Section "Summary"

    Write-Host "Warnings: $($script:Warnings.Count)"

    Write-Host "Failures: $($script:Failures.Count)"



    if ($script:UiSmokeFindings.Count -gt 0) {

        Write-Host ""

        Write-Host "UI smoke findings:"

        $script:UiSmokeFindings | ForEach-Object { Write-Host "- $_" }

    }



    if ($script:Warnings.Count -gt 0) {

        Write-Host ""

        Write-Host "Warnings:"

        $script:Warnings | ForEach-Object { Write-Host "- $_" -ForegroundColor Yellow }

    }



    if ($script:Failures.Count -gt 0) {

        Write-Host ""

        Write-Host "Failures:"

        $script:Failures | ForEach-Object { Write-Host "- $_" -ForegroundColor Red }

        Write-Host ""

        Write-Host "Transcript: $transcriptPath"

        exit 1

    }



    Add-Pass "PMG smoke/regression validation completed successfully."

    Write-Host "Transcript: $transcriptPath"

    exit 0

}

catch {

    Add-Failure "Unhandled validation exception: $($_.Exception.Message)"

    Show-RecentLogs

    Write-Host ""

    Write-Host "Transcript: $transcriptPath"

    exit 1

}

finally {

    try {

        Stop-Transcript | Out-Null

    }

    catch {

        # Ignore transcript stop failures.

    }

}
