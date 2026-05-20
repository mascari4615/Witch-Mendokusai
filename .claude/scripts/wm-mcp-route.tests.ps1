<#
.SYNOPSIS
  Fixture-based smoke tests for wm-mcp-route.ps1 — no Unity required.

.DESCRIPTION
  Creates a temporary scratch directory laid out like a karmoddrine
  multi-worktree environment (main + 2 sibling worktrees), populates each
  with a Library/EditorInstance.json + Library/MCPForUnity/RunState/
  mcp_http_<port>.pid fixture, then exercises the router script:

    Case 1: -Status against a populated worktree → reports correct port.
    Case 2: -DryRun against a populated worktree → does NOT write .mcp.json.
    Case 3: Default write against a populated worktree → writes .mcp.json
            with the worktree-specific port.
    Case 4: Default write against an empty worktree (no PID file) → exits
            with code 2 (cannot route).
    Case 5: Two worktrees with different ports → each .mcp.json gets its own
            port (cross-routing isolation).

  Run from anywhere:
    pwsh -File .claude/scripts/wm-mcp-route.tests.ps1

  This script does NOT depend on Pester; failures throw and the runner exits 1.
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $PSCommandPath
$router = Join-Path $scriptDir 'wm-mcp-route.ps1'
if (-not (Test-Path -LiteralPath $router)) {
    throw "Router script not found: $router"
}

function New-FixtureWorktree {
    param(
        [string]$Root,
        [string]$Name,
        [int]$EditorPid,
        [int]$HttpPort,
        [int]$ServerPid
    )

    $wt = Join-Path $Root $Name
    New-Item -ItemType Directory -Force -Path $wt | Out-Null

    Push-Location $wt
    try {
        & git init --quiet 2>&1 | Out-Null
        & git config user.email "test@example.com" 2>&1 | Out-Null
        & git config user.name "test" 2>&1 | Out-Null
        New-Item -ItemType File -Force -Path (Join-Path $wt '.gitkeep') | Out-Null
        & git add . 2>&1 | Out-Null
        & git commit --quiet -m "init" 2>&1 | Out-Null
    } finally {
        Pop-Location
    }

    if ($EditorPid -gt 0) {
        $libDir = Join-Path $wt 'Library'
        New-Item -ItemType Directory -Force -Path $libDir | Out-Null
        $editorJson = [ordered]@{
            process_id        = $EditorPid
            version           = '6000.0.34f1-test'
            app_path          = 'C:/fake/Editor/Unity.exe'
            app_contents_path = 'C:/fake/Editor/Data'
        } | ConvertTo-Json
        Set-Content -LiteralPath (Join-Path $libDir 'EditorInstance.json') -Value $editorJson -Encoding utf8
    }

    if ($HttpPort -gt 0) {
        $runStateDir = Join-Path $wt 'Library\MCPForUnity\RunState'
        New-Item -ItemType Directory -Force -Path $runStateDir | Out-Null
        $pidFile = Join-Path $runStateDir "mcp_http_$HttpPort.pid"
        Set-Content -LiteralPath $pidFile -Value "$ServerPid" -Encoding utf8
    }

    return $wt
}

function Invoke-Router {
    param(
        [string]$WorktreePath,
        [string[]]$ExtraArgs = @()
    )

    # Prefer pwsh (PowerShell 7+) but fall back to Windows PowerShell.
    $exe = (Get-Command pwsh -ErrorAction SilentlyContinue).Source
    if (-not $exe) {
        $exe = (Get-Command powershell -ErrorAction SilentlyContinue).Source
    }
    if (-not $exe) {
        throw "Neither pwsh nor powershell found on PATH."
    }

    $args = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', $router, '-WorktreePath', $WorktreePath, '-Quiet') + $ExtraArgs
    $output = & $exe @args 2>&1
    return [PSCustomObject]@{
        ExitCode = $LASTEXITCODE
        Output   = ($output | Out-String)
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

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("wm-mcp-route-tests-" + [System.Guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Force -Path $tempRoot | Out-Null

Write-Host "Test fixture root: $tempRoot"

$failures = 0
$passes = 0

try {
    # Fixture A — fully populated worktree on port 8080
    $wtA = New-FixtureWorktree -Root $tempRoot -Name 'wt-a' -EditorPid $PID -HttpPort 8080 -ServerPid $PID

    # Fixture B — fully populated worktree on port 8081 (different port)
    $wtB = New-FixtureWorktree -Root $tempRoot -Name 'wt-b' -EditorPid $PID -HttpPort 8081 -ServerPid $PID

    # Fixture C — no Editor running (no Library at all)
    $wtC = New-FixtureWorktree -Root $tempRoot -Name 'wt-c' -EditorPid 0 -HttpPort 0 -ServerPid 0

    # --- Case 1: -Status reports correct port for wt-a
    $r1 = Invoke-Router -WorktreePath $wtA -ExtraArgs @('-Status')
    Assert ($r1.ExitCode -eq 0) "Case 1 exit code expected 0, got $($r1.ExitCode). Output: $($r1.Output)"
    Assert (-not (Test-Path (Join-Path $wtA '.mcp.json'))) "Case 1: -Status must not write .mcp.json"
    $passes++; Write-Host "[PASS] Case 1: -Status reports without writing"

    # --- Case 2: -DryRun does not write .mcp.json
    $r2 = Invoke-Router -WorktreePath $wtA -ExtraArgs @('-DryRun')
    Assert ($r2.ExitCode -eq 0) "Case 2 exit code expected 0, got $($r2.ExitCode). Output: $($r2.Output)"
    Assert (-not (Test-Path (Join-Path $wtA '.mcp.json'))) "Case 2: -DryRun must not write .mcp.json"
    $passes++; Write-Host "[PASS] Case 2: -DryRun does not write"

    # --- Case 3: default write produces .mcp.json with wt-a's port
    $r3 = Invoke-Router -WorktreePath $wtA
    Assert ($r3.ExitCode -eq 0) "Case 3 exit code expected 0, got $($r3.ExitCode). Output: $($r3.Output)"
    $mcpA = Join-Path $wtA '.mcp.json'
    Assert (Test-Path $mcpA) "Case 3: .mcp.json must exist"
    $jsonA = (Get-Content -LiteralPath $mcpA -Raw) | ConvertFrom-Json
    Assert ($jsonA.mcpServers.unityMCP.type -eq 'http') "Case 3: type must be http"
    Assert ($jsonA.mcpServers.unityMCP.url -eq 'http://127.0.0.1:8080/mcp') "Case 3: url must point to port 8080, got '$($jsonA.mcpServers.unityMCP.url)'"
    $passes++; Write-Host "[PASS] Case 3: default write routes wt-a → 8080"

    # --- Case 4: empty worktree fails with exit code 2
    $r4 = Invoke-Router -WorktreePath $wtC
    Assert ($r4.ExitCode -eq 2) "Case 4 exit code expected 2 (cannot route), got $($r4.ExitCode). Output: $($r4.Output)"
    Assert (-not (Test-Path (Join-Path $wtC '.mcp.json'))) "Case 4: no PID file means no .mcp.json should be written"
    $passes++; Write-Host "[PASS] Case 4: empty worktree exits with 2"

    # --- Case 5: cross-routing isolation between wt-a (port 8080) and wt-b (port 8081)
    $r5 = Invoke-Router -WorktreePath $wtB
    Assert ($r5.ExitCode -eq 0) "Case 5 exit code expected 0, got $($r5.ExitCode). Output: $($r5.Output)"
    $mcpB = Join-Path $wtB '.mcp.json'
    Assert (Test-Path $mcpB) "Case 5: wt-b .mcp.json must exist"
    $jsonB = (Get-Content -LiteralPath $mcpB -Raw) | ConvertFrom-Json
    Assert ($jsonB.mcpServers.unityMCP.url -eq 'http://127.0.0.1:8081/mcp') "Case 5: wt-b url must point to port 8081 (isolated from wt-a's 8080), got '$($jsonB.mcpServers.unityMCP.url)'"
    # And wt-a was unchanged
    $jsonAReread = (Get-Content -LiteralPath $mcpA -Raw) | ConvertFrom-Json
    Assert ($jsonAReread.mcpServers.unityMCP.url -eq 'http://127.0.0.1:8080/mcp') "Case 5: wt-a must remain on 8080 (no cross-contamination)"
    $passes++; Write-Host "[PASS] Case 5: wt-a → 8080, wt-b → 8081 (cross-worktree isolation)"

    # --- Case 6: -Port override forces a custom port
    $r6 = Invoke-Router -WorktreePath $wtA -ExtraArgs @('-Port', '9000')
    Assert ($r6.ExitCode -eq 0) "Case 6 exit code expected 0, got $($r6.ExitCode). Output: $($r6.Output)"
    $jsonAOverride = (Get-Content -LiteralPath $mcpA -Raw) | ConvertFrom-Json
    Assert ($jsonAOverride.mcpServers.unityMCP.url -eq 'http://127.0.0.1:9000/mcp') "Case 6: -Port override must set 9000, got '$($jsonAOverride.mcpServers.unityMCP.url)'"
    $passes++; Write-Host "[PASS] Case 6: -Port override forces custom port"

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
