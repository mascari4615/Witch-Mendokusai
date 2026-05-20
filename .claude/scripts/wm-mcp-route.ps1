<#
.SYNOPSIS
  Worktree-aware Unity-MCP routing — bind the current worktree's Claude session
  to the Unity Editor instance that owns this worktree.

.DESCRIPTION
  Each WitchMendokusai worktree is a self-contained Unity project. When the
  CoplayDev `com.coplaydev.unity-mcp` package is active inside that Editor it
  spawns its own uvx HTTP MCP server and writes a PID file at:

      <worktree>/Library/MCPForUnity/RunState/mcp_http_<port>.pid

  This script:
    1. Resolves the current worktree root (git rev-parse).
    2. Reads Library/EditorInstance.json to confirm a Unity Editor is open
       for this worktree (and which PID).
    3. Discovers the live mcp_http_*.pid file → derives the HTTP port.
    4. Verifies the uvx PID inside the file is alive and the port is bound.
    5. Writes (or updates) <worktree>/.mcp.json so the Claude Code session
       launched from this worktree connects to *this* Editor — not any sibling
       worktree's Editor.

  Mode flags let you status-only, dry-run, or override the port (handy when
  two Editors collide on 8080 and the user has set a custom HttpBaseUrl).

.PARAMETER WorktreePath
  Root of the worktree. Defaults to the cwd (resolved via git rev-parse).

.PARAMETER Port
  Override port discovery. Useful when EditorPrefs HttpBaseUrl was edited
  manually for an Editor that hasn't started its MCP server yet.

.PARAMETER DryRun
  Resolve and print what would be written but do not modify .mcp.json.

.PARAMETER Status
  Print routing diagnostics only (no .mcp.json write attempt).

.PARAMETER All
  Survey every sibling worktree under ../.worktrees and print routing status
  for each. Implies -Status (no writes).

.PARAMETER Quiet
  Suppress informational output; only emit warnings and errors.

.EXAMPLE
  pwsh -File .claude/scripts/wm-mcp-route.ps1
  # default: bind cwd's worktree

.EXAMPLE
  pwsh -File .claude/scripts/wm-mcp-route.ps1 -Status
  # show current routing state without modifying anything

.EXAMPLE
  pwsh -File .claude/scripts/wm-mcp-route.ps1 -All
  # diagnostic survey across every sibling worktree

.EXAMPLE
  pwsh -File .claude/scripts/wm-mcp-route.ps1 -Port 8081
  # force a specific port (e.g. you set custom HttpBaseUrl)
#>

[CmdletBinding()]
param(
    [string]$WorktreePath,
    [int]$Port = 0,
    [switch]$DryRun,
    [switch]$Status,
    [switch]$All,
    [switch]$Quiet
)

$ErrorActionPreference = 'Stop'

function Write-Info {
    param([string]$Message)
    if (-not $Quiet) { Write-Host "[wm-mcp-route] $Message" }
}

function Write-Warn {
    param([string]$Message)
    Write-Warning "[wm-mcp-route] $Message"
}

function Resolve-WorktreeRoot {
    param([string]$StartPath)

    $startDir = if ([string]::IsNullOrWhiteSpace($StartPath)) {
        (Get-Location).Path
    } else {
        $resolved = Resolve-Path -LiteralPath $StartPath -ErrorAction Stop
        $resolved.Path
    }

    try {
        Push-Location -LiteralPath $startDir
        $root = & git rev-parse --show-toplevel 2>$null
        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($root)) {
            throw "Not inside a git worktree: $startDir"
        }
        return ($root.Trim() -replace '/', '\')
    } finally {
        Pop-Location
    }
}

function Read-EditorInstance {
    param([string]$WorktreeRoot)

    $editorInstancePath = Join-Path $WorktreeRoot 'Library\EditorInstance.json'
    if (-not (Test-Path -LiteralPath $editorInstancePath)) {
        return $null
    }

    try {
        $raw = Get-Content -LiteralPath $editorInstancePath -Raw -ErrorAction Stop
        $obj = $raw | ConvertFrom-Json -ErrorAction Stop
    } catch {
        Write-Warn "Failed to parse $editorInstancePath : $($_.Exception.Message)"
        return $null
    }

    $editorPid = 0
    if ($obj.PSObject.Properties.Name -contains 'process_id') {
        $editorPid = [int]$obj.process_id
    }

    return [PSCustomObject]@{
        Path      = $editorInstancePath
        Pid       = $editorPid
        Version   = $obj.version
        AppPath   = $obj.app_path
        IsRunning = if ($editorPid -gt 0) { Test-PidAlive -ProcessId $editorPid } else { $false }
    }
}

function Test-PidAlive {
    param([int]$ProcessId)
    if ($ProcessId -le 0) { return $false }
    try {
        $proc = Get-Process -Id $ProcessId -ErrorAction Stop
        return $null -ne $proc
    } catch {
        return $false
    }
}

function Find-McpPidFiles {
    param([string]$WorktreeRoot)

    $runStateDir = Join-Path $WorktreeRoot 'Library\MCPForUnity\RunState'
    if (-not (Test-Path -LiteralPath $runStateDir)) {
        return @()
    }

    $files = @(Get-ChildItem -LiteralPath $runStateDir -Filter 'mcp_http_*.pid' -ErrorAction SilentlyContinue)
    $result = @()
    foreach ($f in $files) {
        $portFromName = 0
        if ($f.BaseName -match '^mcp_http_(\d+)$') {
            $portFromName = [int]$Matches[1]
        }

        $serverPid = 0
        try {
            $text = (Get-Content -LiteralPath $f.FullName -Raw -ErrorAction Stop).Trim()
            $firstLine = ($text -split "`r?`n" | Where-Object { $_.Trim() })[0]
            if ($firstLine -and [int]::TryParse($firstLine.Trim(), [ref]$serverPid) -eq $false) {
                $serverPid = 0
            }
        } catch {
            $serverPid = 0
        }

        $result += [PSCustomObject]@{
            Path      = $f.FullName
            Port      = $portFromName
            ServerPid = $serverPid
            Alive     = if ($serverPid -gt 0) { Test-PidAlive -ProcessId $serverPid } else { $false }
            Mtime     = $f.LastWriteTimeUtc
        }
    }

    return @($result | Sort-Object Mtime -Descending)
}

function Test-PortListening {
    param([int]$Port)
    if ($Port -le 0) { return $false }
    try {
        $client = [System.Net.Sockets.TcpClient]::new()
        $task = $client.ConnectAsync('127.0.0.1', $Port)
        $ok = $task.Wait(250)
        $connected = $ok -and $client.Connected
        $client.Close()
        return $connected
    } catch {
        return $false
    }
}

function Get-McpRouting {
    param([string]$WorktreeRoot)

    $editor = Read-EditorInstance -WorktreeRoot $WorktreeRoot
    $pidFiles = Find-McpPidFiles -WorktreeRoot $WorktreeRoot

    $candidates = @()
    foreach ($pf in $pidFiles) {
        $listening = Test-PortListening -Port $pf.Port
        $candidates += [PSCustomObject]@{
            Port      = $pf.Port
            ServerPid = $pf.ServerPid
            Alive     = $pf.Alive
            Listening = $listening
            PidFile   = $pf.Path
            Mtime     = $pf.Mtime
        }
    }

    $chosen = $null
    foreach ($c in $candidates) {
        if ($c.Listening) { $chosen = $c; break }
    }
    if ($null -eq $chosen -and $candidates.Count -gt 0) {
        $chosen = $candidates[0]
    }

    return [PSCustomObject]@{
        Worktree    = $WorktreeRoot
        Editor      = $editor
        Candidates  = $candidates
        Chosen      = $chosen
    }
}

function Write-McpConfig {
    param(
        [string]$WorktreeRoot,
        [int]$Port,
        [switch]$DryRun
    )

    if ($Port -le 0) {
        throw "Refusing to write .mcp.json with port=$Port"
    }

    $configPath = Join-Path $WorktreeRoot '.mcp.json'

    $existingPort = 0
    if (Test-Path -LiteralPath $configPath) {
        try {
            $existing = (Get-Content -LiteralPath $configPath -Raw -ErrorAction Stop) | ConvertFrom-Json -ErrorAction Stop
            $url = $existing.mcpServers.unityMCP.url
            if ($url -and ($url -match ':(\d+)/mcp$')) {
                $existingPort = [int]$Matches[1]
            }
        } catch {
            # tolerate malformed existing config; we will overwrite
        }
    }

    if ($existingPort -eq $Port) {
        Write-Info ".mcp.json already routes to port $Port -- no change"
        return [PSCustomObject]@{ Action = 'noop'; Path = $configPath; Port = $Port }
    }

    $payload = [ordered]@{
        mcpServers = [ordered]@{
            unityMCP = [ordered]@{
                type = 'http'
                url  = "http://127.0.0.1:$Port/mcp"
            }
        }
    }

    $json = $payload | ConvertTo-Json -Depth 6

    if ($DryRun) {
        Write-Info "DRY-RUN: would write port $Port to $configPath"
        Write-Info $json
        return [PSCustomObject]@{ Action = 'dryrun'; Path = $configPath; Port = $Port }
    }

    # Write UTF-8 *without* BOM so downstream JSON parsers stay happy.
    $utf8NoBom = [System.Text.UTF8Encoding]::new($false)
    [System.IO.File]::WriteAllText($configPath, $json, $utf8NoBom)
    Write-Info "Wrote port $Port -> $configPath"
    return [PSCustomObject]@{ Action = 'wrote'; Path = $configPath; Port = $Port; PreviousPort = $existingPort }
}

function Format-RoutingReport {
    param([PSCustomObject]$Routing)

    $editorLine = if ($null -eq $Routing.Editor) {
        '  Editor: (no Library/EditorInstance.json -- Editor not running for this worktree)'
    } elseif ($Routing.Editor.IsRunning) {
        "  Editor: PID $($Routing.Editor.Pid) -- $($Routing.Editor.Version) ALIVE"
    } else {
        "  Editor: PID $($Routing.Editor.Pid) -- STALE (process not running)"
    }

    $lines = @(
        "Worktree: $($Routing.Worktree)",
        $editorLine
    )

    if ($Routing.Candidates.Count -eq 0) {
        $lines += '  MCP: no mcp_http_*.pid found (Editor has not started MCP HTTP server yet)'
    } else {
        $lines += "  MCP candidates ($($Routing.Candidates.Count)):"
        foreach ($c in $Routing.Candidates) {
            $marker = if ($Routing.Chosen -and $Routing.Chosen.PidFile -eq $c.PidFile) { '*' } else { ' ' }
            $aliveTag = if ($c.Alive) { 'alive' } else { 'dead' }
            $listenTag = if ($c.Listening) { 'listening' } else { 'closed' }
            $lines += "   $marker port=$($c.Port) uvx-pid=$($c.ServerPid) $aliveTag/$listenTag mtime=$($c.Mtime.ToString('o'))"
        }
    }

    return ($lines -join [Environment]::NewLine)
}

# ---- main ----

if ($All) {
    $here = Resolve-WorktreeRoot -StartPath $WorktreePath
    $worktreesParent = $null

    # Walk up from $here looking for a .worktrees directory either AS the current
    # ancestor or as a sibling of an ancestor.  Covers both:
    #   <root>/<repo>/   (main checkout)
    #   <root>/.worktrees/<wt>/   (sibling worktree)
    $cursor = $here
    while ($cursor) {
        $sibling = Join-Path $cursor '.worktrees'
        if (Test-Path -LiteralPath $sibling) { $worktreesParent = $sibling; break }
        $leaf = Split-Path -Leaf $cursor
        if ($leaf -eq '.worktrees' -and (Test-Path -LiteralPath $cursor)) {
            $worktreesParent = $cursor; break
        }
        $parent = Split-Path -Parent $cursor
        if ($parent -eq $cursor -or [string]::IsNullOrEmpty($parent)) { break }
        $cursor = $parent
    }

    $allRoots = @($here)
    if ($worktreesParent) {
        $allRoots += @(Get-ChildItem -LiteralPath $worktreesParent -Directory | Select-Object -ExpandProperty FullName)
    } else {
        Write-Warn "Could not locate .worktrees ancestor -- surveying current worktree only"
    }
    $allRoots = $allRoots | Select-Object -Unique

    foreach ($root in $allRoots) {
        $report = Format-RoutingReport (Get-McpRouting -WorktreeRoot $root)
        Write-Output $report
        Write-Output ''
    }
    return
}

$worktree = Resolve-WorktreeRoot -StartPath $WorktreePath
$routing = Get-McpRouting -WorktreeRoot $worktree
$report = Format-RoutingReport -Routing $routing
if (-not $Quiet) { Write-Output $report }

if ($Status) {
    return
}

$resolvedPort = if ($Port -gt 0) { $Port } elseif ($routing.Chosen) { $routing.Chosen.Port } else { 0 }

if ($resolvedPort -le 0) {
    Write-Warn "Cannot determine MCP port for $worktree -- open the Unity Editor for this worktree and start the MCP HTTP server (Window > MCP for Unity > Start Server), then re-run."
    exit 2
}

if ($Port -le 0 -and $routing.Chosen -and -not $routing.Chosen.Listening) {
    Write-Warn "Selected port $resolvedPort has no live listener (PID file is stale). The .mcp.json will still be written, but Claude session requests will fail until the Editor starts its MCP HTTP server."
}

$result = Write-McpConfig -WorktreeRoot $worktree -Port $resolvedPort -DryRun:$DryRun
if ($null -ne $result) {
    Write-Output $result
}
