<#
.SYNOPSIS
  Editor.log fallback for compile-error inspection when Unity-MCP `read_console`
  is unavailable — returns only the *latest compile session* lines.

.DESCRIPTION
  CLAUDE.md § Unity-MCP layer states the canonical channel is MCP `read_console`
  because Editor.log is append-only — accumulated past sessions contaminate
  every grep result. The TASK-WM-056-A "259 vs 6" mismatch (2026-05-10) is the
  canonical incident motivating MCP adoption.

  This script makes the *fallback* (when MCP is genuinely unavailable, e.g.
  Editor is launched but no MCP server is running yet, or the user is reading
  a previous session's errors after `unity-refresh.ps1`) accurate by:

    1. Reading the entire Editor.log
    2. Finding the LAST occurrence of the canonical reload marker
       "Reloading assemblies after forced synchronous recompile"
    3. Returning only the lines after that marker that match -Pattern
    4. Reporting the marker timestamp (when available) so callers can sanity
       check that they're looking at a recent compile, not a stale one

  This does NOT replace MCP `read_console`. It is a strictly-scoped fallback
  that avoids the "old errors leaking into current grep" failure mode by
  honouring the same session boundary Unity itself logs.

.PARAMETER LogPath
  Path to Editor.log. Defaults to
  `$env:LOCALAPPDATA\Unity\Editor\Editor.log` (Windows standard).

.PARAMETER Pattern
  Regex used to filter session lines. Default matches compile error/warning
  diagnostics: `error CS|warning CS`. Per CLAUDE.md "Warning 도 무시 X" both
  are surfaced by default.

.PARAMETER MaxLines
  Maximum lines to emit (most recent). 0 = unlimited. Default 200.

.PARAMETER Json
  Emit a structured JSON payload (counts + lines + marker timestamp) instead
  of plain stdout. Useful when calling from another script or agent harness.

.PARAMETER IncludeMarker
  Also include the reload marker line itself at the top of the session
  window (useful for sanity-checking the timestamp).

.OUTPUTS
  Plain mode: one matching line per output line.
  -Json mode: a single-object JSON document with shape
    { logPath, marker, markerLineIndex, totalLines, sessionLines,
      pattern, matched, lines }.

.EXAMPLE
  pwsh -File .claude/scripts/wm-editor-log-tail.ps1
  # last compile session's CS errors + warnings, max 200 lines

.EXAMPLE
  pwsh -File .claude/scripts/wm-editor-log-tail.ps1 -Pattern 'error CS' -MaxLines 50
  # errors only, last 50

.EXAMPLE
  pwsh -File .claude/scripts/wm-editor-log-tail.ps1 -Json | ConvertFrom-Json
  # structured form for tooling

.NOTES
  Exit codes:
    0  marker found, scan succeeded (whether or not anything matched)
    2  Editor.log file not present
    3  Editor.log present but no reload marker found (= no compile session
       has run since the log started; callers should not trust the contents)
#>

[CmdletBinding()]
param(
    [string]$LogPath,
    [string]$Pattern = 'error CS|warning CS',
    [int]$MaxLines = 200,
    [switch]$Json,
    [switch]$IncludeMarker
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($LogPath)) {
    $LogPath = Join-Path $env:LOCALAPPDATA 'Unity\Editor\Editor.log'
}

if (-not (Test-Path -LiteralPath $LogPath)) {
    # $ErrorActionPreference = 'Stop' turns Write-Error into a terminating
    # exception; emit the diagnostic directly to stderr and exit cleanly so
    # callers observe exit code 2 (per .NOTES) instead of a script throw.
    [Console]::Error.WriteLine("[wm-editor-log-tail] Editor.log not found: $LogPath")
    exit 2
}

# Canonical Unity reload marker. CLAUDE.md § Unity-MCP layer cites this exact
# substring as the session boundary signal.
$ReloadMarker = 'Reloading assemblies after forced synchronous recompile'

# Read all lines once; Editor.log is typically a few MB and the cost of one
# read is small compared to repeated greps over the same file.
$allLines = @(Get-Content -LiteralPath $LogPath -ErrorAction Stop)
$totalLines = $allLines.Count

$lastMarkerIdx = -1
for ($i = $totalLines - 1; $i -ge 0; $i--) {
    if ($allLines[$i] -match $ReloadMarker) {
        $lastMarkerIdx = $i
        break
    }
}

if ($lastMarkerIdx -lt 0) {
    if ($Json) {
        $payload = [ordered]@{
            logPath          = $LogPath
            marker           = $null
            markerLineIndex  = -1
            totalLines       = $totalLines
            sessionLines     = 0
            pattern          = $Pattern
            matched          = 0
            lines            = @()
            warning          = "No '$ReloadMarker' marker found — log may be from a session that never reloaded assemblies. Results would be unreliable; emitting empty set."
        }
        $payload | ConvertTo-Json -Depth 4
    } else {
        Write-Warning "No '$ReloadMarker' marker found in $LogPath — log unreliable, emitting empty result."
    }
    exit 3
}

# Slice = [marker .. end]. If IncludeMarker is off we drop the marker line
# itself so the output is only payload lines (errors/warnings).
$sliceStart = if ($IncludeMarker) { $lastMarkerIdx } else { $lastMarkerIdx + 1 }
if ($sliceStart -ge $totalLines) {
    $sessionLines = @()
} else {
    $sessionLines = @($allLines[$sliceStart..($totalLines - 1)])
}

# Coerce to plain strings — Get-Content attaches ETS metadata (PSPath,
# PSProvider, etc.) to each line, and ConvertTo-Json would otherwise expand
# those into nested objects rather than emitting a clean string array.
$matchedLines = @($sessionLines | Where-Object { $_ -match $Pattern } | ForEach-Object { [string]$_ })

if ($MaxLines -gt 0 -and $matchedLines.Count -gt $MaxLines) {
    $matchedLines = @($matchedLines | Select-Object -Last $MaxLines)
}

if ($Json) {
    $payload = [ordered]@{
        logPath          = $LogPath
        marker           = $ReloadMarker
        markerLineIndex  = $lastMarkerIdx
        totalLines       = $totalLines
        sessionLines     = $sessionLines.Count
        pattern          = $Pattern
        matched          = $matchedLines.Count
        lines            = $matchedLines
    }
    $payload | ConvertTo-Json -Depth 4
} else {
    foreach ($line in $matchedLines) {
        Write-Output $line
    }
}
exit 0
