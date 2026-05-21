<#
.SYNOPSIS
  Fixture-based smoke tests for wm-editor-log-tail.ps1 — no Unity required.

.DESCRIPTION
  Writes a synthetic Editor.log with two compile sessions back-to-back (the
  exact failure shape TASK-WM-056-A documented, where the script SHOULD return
  only the latest session's errors), then exercises the helper:

    Case 1: Default pattern returns only session-2 lines (not session-1's
            errors that grep against the whole file would otherwise leak).
    Case 2: -MaxLines caps output to the most-recent N matches.
    Case 3: -Json emits structured payload with correct counts.
    Case 4: -Pattern 'error CS' excludes warnings.
    Case 5: -IncludeMarker prepends the reload marker.
    Case 6: Log with no marker exits with code 3 + empty result.
    Case 7: Missing log file exits with code 2.

  Run from anywhere:
    pwsh -File .claude/scripts/wm-editor-log-tail.tests.ps1

  This script does NOT depend on Pester; failures throw and the runner exits 1.
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $PSCommandPath
$helper = Join-Path $scriptDir 'wm-editor-log-tail.ps1'
if (-not (Test-Path -LiteralPath $helper)) {
    throw "Helper script not found: $helper"
}

function New-FixtureLog {
    param(
        [string]$Path,
        [string[]]$Lines
    )
    $dir = Split-Path -Parent $Path
    if (-not (Test-Path -LiteralPath $dir)) {
        New-Item -ItemType Directory -Force -Path $dir | Out-Null
    }
    Set-Content -LiteralPath $Path -Value $Lines -Encoding utf8
}

function Invoke-Helper {
    param(
        [string]$LogPath,
        [string[]]$ExtraArgs = @()
    )
    $exe = (Get-Command pwsh -ErrorAction SilentlyContinue).Source
    if (-not $exe) {
        $exe = (Get-Command powershell -ErrorAction SilentlyContinue).Source
    }
    if (-not $exe) {
        throw "Neither pwsh nor powershell found on PATH."
    }
    $args = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $helper, '-LogPath', $LogPath) + $ExtraArgs

    # CLAUDE.md: avoid `2>&1` on native exes — Windows PowerShell wraps each
    # stderr line in a NativeCommandError, and under $ErrorActionPreference =
    # 'Stop' that terminates the test runner before we can assert on exit code.
    # Route stderr to a temp file (separate stream redirection avoids the
    # NativeCommandError wrapping). Use call-operator `&` so PowerShell handles
    # argument quoting for items containing spaces (Start-Process ArgumentList
    # would split 'error CS' across two args).
    $stderrFile = [System.IO.Path]::GetTempFileName()
    $prevPref = $ErrorActionPreference
    try {
        # PowerShell 5.1 surfaces stderr lines from a child native exe as
        # ErrorRecord even when redirected; under Stop preference that aborts
        # the test runner. Relax to Continue only for the child invocation so
        # we can capture exit code and stderr without throw.
        $ErrorActionPreference = 'Continue'
        $stdoutLines = & $exe @args 2>$stderrFile
        $exitCode = $LASTEXITCODE
        $stdout = ($stdoutLines | Out-String)
        $stderr = ''
        if (Test-Path -LiteralPath $stderrFile) {
            $rawErr = Get-Content -LiteralPath $stderrFile -Raw -ErrorAction SilentlyContinue
            if ($null -ne $rawErr) { $stderr = $rawErr }
        }
        return [PSCustomObject]@{
            ExitCode = $exitCode
            Output   = $stdout.TrimEnd("`r", "`n")
            Stderr   = $stderr.TrimEnd("`r", "`n")
        }
    } finally {
        $ErrorActionPreference = $prevPref
        Remove-Item -LiteralPath $stderrFile -ErrorAction SilentlyContinue
    }
}

function Assert {
    param(
        [bool]$Condition,
        [string]$Message
    )
    if (-not $Condition) {
        throw "ASSERT FAILED: $Message"
    }
}

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("wm-editor-log-tail-tests-" + [System.Guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Force -Path $tempRoot | Out-Null

Write-Host "Test fixture root: $tempRoot"

$passes = 0
$failures = 0

try {
    # Two-session fixture — session 1 has OLD errors (should NOT leak),
    # session 2 has NEW errors (should be returned). This matches the exact
    # leak pattern TASK-WM-056-A caught (259 vs 6).
    $twoSession = Join-Path $tempRoot 'two-session.log'
    New-FixtureLog -Path $twoSession -Lines @(
        '--- session 1 (old) ---',
        'Reloading assemblies after forced synchronous recompile.',
        "Assets/Old.cs(10,5): error CS1234: old error A",
        "Assets/Old.cs(11,5): error CS1234: old error B",
        "Assets/Old.cs(12,5): warning CS9999: old warning",
        'Compilation succeeded -- some progress',
        '--- session 2 (latest) ---',
        'Reloading assemblies after forced synchronous recompile.',
        "Assets/New.cs(1,1): error CS0246: new error X",
        "Assets/New.cs(2,2): warning CS0168: new warning Y",
        'Compilation succeeded',
        '... end of log ...'
    )

    # --- Case 1: default pattern returns only session 2 lines
    $r1 = Invoke-Helper -LogPath $twoSession
    Assert ($r1.ExitCode -eq 0) "Case 1 exit code expected 0, got $($r1.ExitCode). Output: $($r1.Output)"
    Assert ($r1.Output -match 'new error X') "Case 1: must include 'new error X'. Output: $($r1.Output)"
    Assert ($r1.Output -match 'new warning Y') "Case 1: must include 'new warning Y'. Output: $($r1.Output)"
    Assert ($r1.Output -notmatch 'old error A') "Case 1: must NOT include 'old error A' (session-1 leak). Output: $($r1.Output)"
    Assert ($r1.Output -notmatch 'old error B') "Case 1: must NOT include 'old error B'. Output: $($r1.Output)"
    Assert ($r1.Output -notmatch 'old warning') "Case 1: must NOT include 'old warning'. Output: $($r1.Output)"
    $passes++; Write-Host "[PASS] Case 1: returns only latest compile session"

    # --- Case 2: -MaxLines 1 keeps only the most-recent match
    $r2 = Invoke-Helper -LogPath $twoSession -ExtraArgs @('-MaxLines', '1')
    Assert ($r2.ExitCode -eq 0) "Case 2 exit code expected 0, got $($r2.ExitCode). Output: $($r2.Output)"
    $lineCount = ($r2.Output -split "`r?`n" | Where-Object { $_.Trim() }).Count
    Assert ($lineCount -eq 1) "Case 2: -MaxLines 1 must return exactly 1 line, got $lineCount. Output: $($r2.Output)"
    # most-recent of (X, Y) appears later in file so should be Y
    Assert ($r2.Output -match 'new warning Y') "Case 2: most-recent match should be 'new warning Y'. Output: $($r2.Output)"
    $passes++; Write-Host "[PASS] Case 2: -MaxLines caps output"

    # --- Case 3: -Json emits structured payload
    $r3 = Invoke-Helper -LogPath $twoSession -ExtraArgs @('-Json')
    Assert ($r3.ExitCode -eq 0) "Case 3 exit code expected 0, got $($r3.ExitCode). Output: $($r3.Output)"
    $obj = $r3.Output | ConvertFrom-Json
    Assert ($obj.matched -eq 2) "Case 3: matched count should be 2, got $($obj.matched). Output: $($r3.Output)"
    Assert ($obj.sessionLines -ge 3) "Case 3: sessionLines should include session-2 body, got $($obj.sessionLines)"
    Assert ($obj.markerLineIndex -gt 0) "Case 3: markerLineIndex must be positive (latest marker), got $($obj.markerLineIndex)"
    Assert ($obj.pattern -eq 'error CS|warning CS') "Case 3: pattern should round-trip"
    Assert ((($obj.lines -join ' ') -match 'new error X')) "Case 3: JSON lines must include 'new error X'"
    $passes++; Write-Host "[PASS] Case 3: -Json emits structured payload"

    # --- Case 4: -Pattern 'error CS' excludes warnings
    $r4 = Invoke-Helper -LogPath $twoSession -ExtraArgs @('-Pattern', 'error CS')
    Assert ($r4.ExitCode -eq 0) "Case 4 exit code expected 0, got $($r4.ExitCode)"
    Assert ($r4.Output -match 'new error X') "Case 4: must include 'new error X'"
    Assert ($r4.Output -notmatch 'new warning Y') "Case 4: must NOT include warnings. Output: $($r4.Output)"
    $passes++; Write-Host "[PASS] Case 4: -Pattern filters out warnings"

    # --- Case 5: -IncludeMarker prepends the reload marker line
    $r5 = Invoke-Helper -LogPath $twoSession -ExtraArgs @('-IncludeMarker', '-Pattern', 'Reloading|error CS|warning CS')
    Assert ($r5.ExitCode -eq 0) "Case 5 exit code expected 0, got $($r5.ExitCode)"
    $firstNonEmpty = ($r5.Output -split "`r?`n" | Where-Object { $_.Trim() })[0]
    Assert ($firstNonEmpty -match 'Reloading assemblies after forced synchronous recompile') "Case 5: first matching line must be the marker. Output: $($r5.Output)"
    $passes++; Write-Host "[PASS] Case 5: -IncludeMarker surfaces marker line"

    # --- Case 6: log with NO marker exits 3 + empty
    $noMarker = Join-Path $tempRoot 'no-marker.log'
    New-FixtureLog -Path $noMarker -Lines @(
        'Some Unity boot output',
        'Loading some package',
        'Project loaded'
    )
    $r6 = Invoke-Helper -LogPath $noMarker
    Assert ($r6.ExitCode -eq 3) "Case 6 exit code expected 3 (no marker), got $($r6.ExitCode). Output: $($r6.Output)"
    $passes++; Write-Host "[PASS] Case 6: log without reload marker exits 3"

    # --- Case 7: missing log file exits 2
    $missing = Join-Path $tempRoot 'does-not-exist.log'
    $r7 = Invoke-Helper -LogPath $missing
    Assert ($r7.ExitCode -eq 2) "Case 7 exit code expected 2 (missing log), got $($r7.ExitCode). Output: $($r7.Output)"
    $passes++; Write-Host "[PASS] Case 7: missing log exits 2"

    Write-Host ""
    Write-Host "Result: $passes passed, $failures failed"
} catch {
    $failures++
    Write-Host ""
    Write-Host "Result: $passes passed, $failures failed"
    Write-Host "ERROR: $($_.Exception.Message)"
    Write-Host $_.ScriptStackTrace
    exit 1
} finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

if ($failures -gt 0) { exit 1 }
exit 0
