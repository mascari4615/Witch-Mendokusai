# wm-rule-gate.ps1 -- deterministic WM code-rule gate (TASK-WM-203).
#
# Canonical rule text: WitchMendokusai/CLAUDE.md, section "coding style" / "Editor menu"
# / "input system". This script is the *enforcement* of those rules. It is called from
# two seams so the rule fires without anyone remembering it:
#
#   1. local  : .git hook pre-push  (fast loop -- catches it before it leaves the machine)
#   2. remote : .github/workflows/wm-quality-gate.yml (authority -- catches it on main/PR)
#
# Why grep and not a Roslyn analyzer: the analyzer path (.editorconfig +
# Directory.Build.props, TASK-WM-056-E1) only fires under `dotnet build`, and that path
# was retired 2026-05-10 (Unity Mono != .NET 8, false confidence). So the analyzer config
# is an editor hint today, and *this* script is what actually holds the line.
#
# ASCII-only on purpose: PowerShell 5.1 on a Korean Windows box reads BOM-less files as
# cp949 and mangles non-ASCII, which has already killed one workflow (TASK-WM-197).
#
# Usage:
#   pwsh -File .github/scripts/wm-rule-gate.ps1              # scan all
#   pwsh -File .github/scripts/wm-rule-gate.ps1 -MaxShown 5  # trim output
# Exit code: 0 = clean, 1 = violations found (this is the gate).

[CmdletBinding()]
param(
    [string]$Root,
    [int]$MaxShown = 20,
    # Commit-scoped mode: judge the *content being pushed*, not whatever happens to sit in
    # the working tree. Without this, one session's half-finished edits block another
    # session's unrelated push -- reported from the field within an hour of shipping the hook.
    [string]$Sha,
    [string[]]$Paths
)

$ErrorActionPreference = 'Stop'

$commitScoped = -not [string]::IsNullOrWhiteSpace($Sha)

if (-not $commitScoped)
{
    if ([string]::IsNullOrWhiteSpace($Root))
    {
        $repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
        $Root = Join-Path $repoRoot 'Assets/_WitchMendokusai'
    }

    if (-not (Test-Path $Root))
    {
        Write-Host "wm-rule-gate: scan root not found: $Root"
        exit 1
    }
}

# Unity's own menu roots are legitimate extension points -- the WM rule is about the
# project's *own* top-level menu being a single "WM/" entry, not about these.
$menuRootAllow = @('WM', 'Assets', 'GameObject', 'CONTEXT', 'Component', 'Window', 'Help', 'Edit', 'File', 'Tools')

# Reading a device directly is allowed *inside* the input encapsulation boundary only.
$deviceAllowedPathFragment = 'Core/Scripts/Input/'

$rules = @(
    @{
        Id      = 'VAR'
        Title   = "'var' is banned -- always write the explicit type"
        Fix     = 'replace var with the concrete type'
        Strings = $false
        Match   = { param($line) $line -match '(^|[^\w.])var\s+[\w(]' }
    },
    @{
        Id      = 'NOT-OP'
        Title   = "negative condition must be '== false', not '!'"
        Fix     = 'rewrite `if (!x)` as `if (x == false)`'
        Strings = $false
        Match   = { param($line) $line -match '(if|while)\s*\(\s*!\s*[A-Za-z_(]' -or $line -match '(&&|\|\|)\s*!\s*[A-Za-z_(]' }
    },
    @{
        Id      = 'LEGACY-INPUT'
        Title   = 'legacy Input API is banned -- use the New Input System'
        Fix     = 'route through InputManager.RegisterInputEvent'
        Strings = $false
        Match   = { param($line) $line -match '(^|[^\w.])Input\.(GetKey|GetAxis|GetButton|GetMouseButton|mousePosition|mouseScrollDelta|touches|touchCount|anyKey)' }
    },
    @{
        Id      = 'RAW-DEVICE'
        Title   = 'game components must not read input devices directly'
        Fix     = 'expose it through InputManager instead'
        Strings = $false
        Match   = { param($line) $line -match '(Keyboard|Mouse|Gamepad|Touchscreen)\.current' }
        SkipPath = $deviceAllowedPathFragment
    },
    @{
        Id      = 'MENU-ROOT'
        Title   = 'Editor menu root must be "WM/"'
        Fix     = 'move the MenuItem under WM/'
        Strings = $true
        Match   = {
            param($line)
            if ($line -notmatch 'MenuItem\s*\(\s*"([^"]+)"') { return $false }
            $path = $Matches[1]
            $rootSegment = ($path -split '/')[0]
            return ($menuRootAllow -notcontains $rootSegment)
        }
    }
)

# Comments are not code. An earlier hand-grep flagged commented-out `var` lines as
# violations -- a gate that cries wolf gets switched off, so strip them first.
function Get-CodeText
{
    param([string]$Line, [bool]$KeepStrings)

    $text = $Line -replace '//.*$', ''
    if ($text -match '^\s*(\*|/\*)') { return '' }
    if ($text -match '^\s*#') { return '' }   # preprocessor: #if !UNITY_EDITOR is not a C# condition
    if (-not $KeepStrings)
    {
        $text = $text -replace '"[^"]*"', '""'
    }
    return $text
}

$findings = @{}
foreach ($rule in $rules) { $findings[$rule.Id] = New-Object System.Collections.ArrayList }

# Two sources of truth, one rule set:
#   working tree (default) -- what a human sees in the editor right now
#   commit  (-Sha/-Paths)  -- what is actually leaving the machine / sitting on main
$subjects = New-Object System.Collections.ArrayList
if ($commitScoped)
{
    foreach ($path in $Paths)
    {
        if ([string]::IsNullOrWhiteSpace($path)) { continue }
        $blob = & git show "${Sha}:${path}" 2>$null
        if ($LASTEXITCODE -ne 0) { continue }   # deleted in this commit -- nothing to judge
        $relative = $path -replace '^Assets/_WitchMendokusai/', ''
        [void]$subjects.Add([pscustomobject]@{ Relative = $relative; Lines = @($blob) })
    }
}
else
{
    $rootFull = (Resolve-Path $Root).Path
    foreach ($file in (Get-ChildItem -Path $Root -Filter *.cs -Recurse -File))
    {
        $relative = $file.FullName.Substring($rootFull.Length).TrimStart('\', '/').Replace('\', '/')
        [void]$subjects.Add([pscustomobject]@{
            Relative = $relative
            Lines    = [System.IO.File]::ReadAllLines($file.FullName)
        })
    }
}

foreach ($file in $subjects)
{
    $relative = $file.Relative
    $lines = $file.Lines

    for ($i = 0; $i -lt $lines.Length; $i++)
    {
        $raw = $lines[$i]
        if ([string]::IsNullOrWhiteSpace($raw)) { continue }

        foreach ($rule in $rules)
        {
            if ($rule.ContainsKey('SkipPath') -and $relative -like ("*" + $rule.SkipPath + "*")) { continue }

            $code = Get-CodeText -Line $raw -KeepStrings $rule.Strings
            if ([string]::IsNullOrWhiteSpace($code)) { continue }

            if (& $rule.Match $code)
            {
                [void]$findings[$rule.Id].Add(("{0}:{1}: {2}" -f $relative, ($i + 1), $raw.Trim()))
            }
        }
    }
}

# ---------------------------------------------------------------------------
# ANCHOR -- "must still be there" checks.
#
# The rules above catch forbidden lines. These catch the opposite failure:
# a required wiring line that silently DISAPPEARS. That happened three times in
# one day (2026-08-06) -- a wide sweeping commit (warning cleanup / format /
# bulk rename) laid down a stale copy and one call vanished. Compile stays green,
# tests stay green, and the feature is simply gone until a human plays the game.
#
# Keep this list SHORT: only lines that (a) a user explicitly asked for and
# (b) die silently when removed. A fat list blocks honest refactoring.
# ---------------------------------------------------------------------------
$anchors = @(
    @{ File = 'Domain/UI/UIRoot.cs'
       Needle = 'AddComponent<MobileControlsView>'
       Why = 'phone controls are never created -- the screen shows but nothing is touchable' },
    @{ File = 'Domain/UI/Hub/UIMinigameHubToolkit.cs'
       Needle = 'NavigationMoveEvent'
       Why = 'cannot move focus from the list to the start button' },
    @{ File = 'Domain/TowerDefense/TowerDefensePlacement.cs'
       Needle = 'aboveFog: true'
       Why = 'the cursor marker hides under the fog -- cannot build on unexplored ground' },
    @{ File = 'Domain/TowerDefense/TowerDefenseMatch.cs'
       Needle = 'heroMovement.SetMoveDirection'
       Why = 'hero moves by raw transform again -- stutters, walks through walls, pushes monsters' }
)

$anchorMisses = New-Object System.Collections.ArrayList
foreach ($anchor in $anchors)
{
    $full = Join-Path $Root $anchor.File
    if (-not (Test-Path $full))
    {
        [void]$anchorMisses.Add(("{0} -- file missing (moved? update this gate too)" -f $anchor.File))
        continue
    }
    $text = Get-Content -Raw -LiteralPath $full
    if ($text -notlike ("*" + $anchor.Needle + "*"))
    {
        [void]$anchorMisses.Add(("{0} -- lost '{1}': {2}" -f $anchor.File, $anchor.Needle, $anchor.Why))
    }
}

$total = 0
foreach ($rule in $rules) { $total += $findings[$rule.Id].Count }
$total += $anchorMisses.Count

if ($commitScoped)
{
    $shortSha = if ($Sha.Length -ge 8) { $Sha.Substring(0, 8) } else { $Sha }
    Write-Host "wm-rule-gate -- scanned $($subjects.Count) .cs file(s) as committed in $shortSha"
}
else
{
    Write-Host "wm-rule-gate -- scanned $($subjects.Count) .cs files under $Root"
}
Write-Host ''

foreach ($rule in $rules)
{
    $hits = $findings[$rule.Id]
    if ($hits.Count -eq 0)
    {
        Write-Host ("  PASS  [{0}] {1}" -f $rule.Id, $rule.Title)
        continue
    }

    Write-Host ("  FAIL  [{0}] {1} -- {2} hit(s); fix: {3}" -f $rule.Id, $rule.Title, $hits.Count, $rule.Fix)
    $shown = 0
    foreach ($hit in $hits)
    {
        Write-Host ("          " + $hit)
        $shown++
        if ($shown -ge $MaxShown)
        {
            Write-Host ("          ... and {0} more" -f ($hits.Count - $shown))
            break
        }
    }
}

Write-Host ''
if ($total -eq 0)
{
    Write-Host '  PASS  [ANCHOR] required wiring lines are still present'
    Write-Host 'RESULT: PASS -- 0 rule violations.'
    exit 0
}

if ($anchorMisses.Count -eq 0)
{
    Write-Host '  PASS  [ANCHOR] required wiring lines are still present'
}
else
{
    Write-Host ("  FAIL  [ANCHOR] required wiring line(s) disappeared -- {0} hit(s); fix: put the line back (do not delete it)" -f $anchorMisses.Count)
    foreach ($miss in $anchorMisses) { Write-Host ("          " + $miss) }
}

Write-Host ("RESULT: FAIL -- {0} rule violation(s). Rule text: WitchMendokusai/CLAUDE.md" -f $total)
exit 1
