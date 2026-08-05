# Tools/git-hooks/wm-commit-verify.ps1 — TASK-WM-109-F
#
# Lightweight post-commit verification for WitchMendokusai.
#
# Responsibilities:
#   1. Append commit metadata row to <git-common-dir>/wm-commit-log.tsv (ledger).
#   2. Count touched .cs / .meta / .asset / .prefab / .unity files in the commit.
#   3. Pair-check .cs (added/modified) ↔ .cs.meta — Unity GUID-loss canary.
#   4. Heuristic "big commit" advisory when .cs count exceeds a small threshold.
#   5. TCP probe of Unity-MCP (port from .mcp.json; informational only — no MCP RPC).
#
# Non-responsibilities:
#   - Calling Unity (compile / Play). Hooks must be fast; canonical compile
#     verification stays MCP `read_console` (CLAUDE.md § Unity-MCP layer).
#   - Blocking commits — this is post-commit; runs always with exit 0.
#
# Invoked by .git/hooks/post-commit after install.ps1 sets it up.

[CmdletBinding()]
param(
    [string]$Sha = $env:WM_COMMIT_SHA,
    [int]$BigCommitCsThreshold = 10
)

$ErrorActionPreference = 'Continue'

function Resolve-RepoPaths
{
    $rootRaw = (git rev-parse --show-toplevel 2>$null)
    if ([string]::IsNullOrWhiteSpace($rootRaw))
    {
        return $null
    }
    $commonRaw = (git rev-parse --git-common-dir 2>$null)
    if ([string]::IsNullOrWhiteSpace($commonRaw))
    {
        return $null
    }

    $root = $rootRaw.Trim()
    $common = $commonRaw.Trim()
    if (-not [System.IO.Path]::IsPathRooted($common))
    {
        $common = [System.IO.Path]::GetFullPath((Join-Path $root $common))
    }
    return [pscustomobject]@{
        Root      = $root
        CommonDir = $common
    }
}

$paths = Resolve-RepoPaths
if ($null -eq $paths)
{
    Write-Host "[wm-verify] not in a git repo — skip"
    exit 0
}

if ([string]::IsNullOrWhiteSpace($Sha))
{
    $shaRaw = (git rev-parse HEAD 2>$null)
    if ([string]::IsNullOrWhiteSpace($shaRaw))
    {
        Write-Host "[wm-verify] no HEAD — skip"
        exit 0
    }
    $Sha = $shaRaw.Trim()
}

$shortSha = $Sha.Substring(0, [Math]::Min(12, $Sha.Length))
$subject = (git log -1 --format=%s $Sha 2>$null)
$author = (git log -1 --format=%an $Sha 2>$null)
$parentCount = ((git log -1 --format=%P $Sha 2>$null) -split '\s+' | Where-Object { $_ -ne '' }).Count
$tsIso = (Get-Date).ToString('yyyy-MM-ddTHH:mm:sszzz')

# Diff name-status — skip for merge commits (multi-parent: nothing meaningful
# to count, and `git show --name-status` semantics differ).
$cs = @()
$meta = @()
$asset = @()
$prefab = @()
$scene = @()

if ($parentCount -le 1)
{
    $rawOutput = git show --name-status --format= $Sha 2>$null
    $lines = @()
    if ($null -ne $rawOutput)
    {
        foreach ($entry in @($rawOutput))
        {
            foreach ($line in ($entry -split "`n"))
            {
                if (-not [string]::IsNullOrWhiteSpace($line))
                {
                    $lines += $line
                }
            }
        }
    }

    foreach ($line in $lines)
    {
        if ($line -match '\t.+\.cs$')     { $cs     += $line; continue }
        if ($line -match '\t.+\.meta$')   { $meta   += $line; continue }
        if ($line -match '\t.+\.asset$')  { $asset  += $line; continue }
        if ($line -match '\t.+\.prefab$') { $prefab += $line; continue }
        if ($line -match '\t.+\.unity$')  { $scene  += $line; continue }
    }
}

$csCount = $cs.Count
$metaCount = $meta.Count
$assetCount = $asset.Count
$prefabCount = $prefab.Count
$sceneCount = $scene.Count

# .cs (added/modified) ↔ .cs.meta pair check. Renames (R*) are ignored — the
# original .meta likely already exists; git rename detection handles them.
$metaPathsInCommit = $meta | ForEach-Object { ($_ -split "`t", 2)[1] }
$csMissingMeta = @()
foreach ($line in $cs)
{
    $parts = $line -split "`t", 2
    if ($parts.Count -lt 2) { continue }
    $status = $parts[0]
    $path = $parts[1]
    if ($status -notmatch '^[AM]') { continue }

    $expectedMeta = "$path.meta"
    if ($metaPathsInCommit -contains $expectedMeta) { continue }

    # Sometimes .meta was committed in a prior commit and remains on disk.
    # Only flag when there is genuinely no .meta on disk.
    $absMeta = Join-Path $paths.Root $expectedMeta
    if (-not (Test-Path -LiteralPath $absMeta))
    {
        $csMissingMeta += $path
    }
}

$bigCommit = $csCount -gt $BigCommitCsThreshold

# --- 은퇴한 식별자 canary (stale-copy 되돌림 탐지) ---
# 개명은 커밋 하나로 끝나지만, 다른 세션의 작업트리가 옛 사본을 들고 있으면 그 세션이 다음에
# 그 파일을 통째로 쓰는 순간 옛 이름이 *추가된 줄*로 되살아난다. 그날 main 은 CS0246 로 죽는다
# (2026-08-06 하루에 네 번). 추가된 줄만 보므로 개명 커밋 자신은 안 걸린다.
# 막지 않고 알리기만 한다 — 진짜로 옛 이름을 되살리려는 커밋일 수도 있다.
$retiredHits = @()
$retiredFile = Join-Path $paths.Root 'Tools/git-hooks/retired-identifiers.tsv'
if ($parentCount -le 1 -and $csCount -gt 0 -and (Test-Path -LiteralPath $retiredFile))
{
    $rules = @()
    foreach ($ruleLine in @(Get-Content -LiteralPath $retiredFile -Encoding UTF8))
    {
        if ([string]::IsNullOrWhiteSpace($ruleLine) -or $ruleLine.TrimStart().StartsWith('#')) { continue }
        $fields = $ruleLine -split "`t"
        if ($fields.Count -ge 2 -and -not [string]::IsNullOrWhiteSpace($fields[0]))
        {
            $rules += [pscustomobject]@{ Old = $fields[0].Trim(); New = $fields[1].Trim() }
        }
    }

    if ($rules.Count -gt 0)
    {
        $addedLines = @()
        foreach ($entry in @(git show --unified=0 --format= $Sha -- '*.cs' 2>$null))
        {
            foreach ($line in ($entry -split "`n"))
            {
                if ($line.StartsWith('+') -and -not $line.StartsWith('+++')) { $addedLines += $line }
            }
        }

        foreach ($rule in $rules)
        {
            $pattern = '\b' + [regex]::Escape($rule.Old) + '\b'
            $hitCount = @($addedLines | Where-Object { $_ -match $pattern }).Count
            if ($hitCount -gt 0)
            {
                $retiredHits += [pscustomobject]@{ Old = $rule.Old; New = $rule.New; Count = $hitCount }
            }
        }
    }
}

# Unity-MCP TCP probe — fast (~300ms timeout). Informational only.
# 포트 정본 = .mcp.json (하드코딩하면 사용자가 포트를 바꾼 날부터 영영 "미응답"만 찍는다 —
# 실제로 8080 이 박혀 있어 12345 로 옮긴 뒤 모든 커밋이 거짓 경고를 달고 있었다).
$mcpPort = 12345
$mcpJson = Join-Path $paths.Root '.mcp.json'
if (Test-Path -LiteralPath $mcpJson)
{
    $portMatch = [regex]::Match((Get-Content -LiteralPath $mcpJson -Raw), ':(\d+)/mcp')
    if ($portMatch.Success) { $mcpPort = [int]$portMatch.Groups[1].Value }
}

$mcpUp = $false
try
{
    $tcp = New-Object System.Net.Sockets.TcpClient
    $iar = $tcp.BeginConnect('127.0.0.1', $mcpPort, $null, $null)
    if ($iar.AsyncWaitHandle.WaitOne(300, $false))
    {
        try
        {
            $tcp.EndConnect($iar)
            $mcpUp = $tcp.Connected
        }
        catch
        {
            $mcpUp = $false
        }
    }
    $tcp.Close()
}
catch
{
    $mcpUp = $false
}

# Ledger — TSV, header on first write.
$ledger = Join-Path $paths.CommonDir 'wm-commit-log.tsv'
$header = "ts`tsha`tauthor`tcs`tmeta`tasset`tprefab`tscene`tmcp`tparents`tsubject"
if (-not (Test-Path -LiteralPath $ledger))
{
    Set-Content -LiteralPath $ledger -Value $header -Encoding UTF8
}
$cleanSubject = ($subject -replace "`t", ' ').Trim()
$row = "$tsIso`t$shortSha`t$author`t$csCount`t$metaCount`t$assetCount`t$prefabCount`t$sceneCount`t$([int]$mcpUp)`t$parentCount`t$cleanSubject"
Add-Content -LiteralPath $ledger -Value $row -Encoding UTF8

# Console summary — single line headline + optional advisories.
$tag = if ($parentCount -gt 1) { '[merge]' } elseif ($bigCommit) { '[big]' } else { '[ok]' }
Write-Host "[wm-verify] $tag $shortSha  cs=$csCount meta=$metaCount asset=$assetCount prefab=$prefabCount scene=$sceneCount  mcp=$([int]$mcpUp)"

if ($bigCommit)
{
    Write-Host "[wm-verify]   hint: $csCount .cs in one commit — bisect 비용 ↑. 다음엔 단위 분할 검토 (Tools/git-hooks/README.md § 커밋 규율)."
}

if ($retiredHits.Count -gt 0)
{
    Write-Host "[wm-verify]   ★ 은퇴한 이름이 *추가된 줄*에 있다 — 옛 사본을 들고 있을 가능성:"
    foreach ($hit in $retiredHits)
    {
        Write-Host "[wm-verify]     - $($hit.Old) x$($hit.Count)  ->  $($hit.New)"
    }
    Write-Host "[wm-verify]     확인: git diff origin/main -- <이번에 만진 파일>  (내가 안 만진 줄이 바뀌었나)"
    Write-Host "[wm-verify]     정본: Tools/git-hooks/retired-identifiers.tsv"
}

if ($csMissingMeta.Count -gt 0)
{
    Write-Host "[wm-verify]   warn: .cs add/modify 인데 짝 .meta 부재 (Unity GUID 누락 가능):"
    foreach ($p in ($csMissingMeta | Select-Object -First 5))
    {
        Write-Host "[wm-verify]     - $p"
    }
    if ($csMissingMeta.Count -gt 5)
    {
        Write-Host "[wm-verify]     ... +$($csMissingMeta.Count - 5) more"
    }
}

if (-not $mcpUp)
{
    Write-Host "[wm-verify]   info: Unity-MCP :$mcpPort 미응답 — 다음 commit 전 Editor Console / read_console 으로 컴파일 검증 권장."
}

exit 0
