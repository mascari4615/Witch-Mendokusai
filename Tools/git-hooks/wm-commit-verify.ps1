# Tools/git-hooks/wm-commit-verify.ps1 — TASK-WM-109-F
#
# Lightweight post-commit verification for WitchMendokusai.
#
# Responsibilities:
#   1. Append commit metadata row to <git-common-dir>/wm-commit-log.tsv (ledger).
#   2. Count touched .cs / .meta / .asset / .prefab / .unity files in the commit.
#   3. Pair-check .cs (added/modified) ↔ .cs.meta — Unity GUID-loss canary.
#   4. Heuristic "big commit" advisory when .cs count exceeds a small threshold.
#   5. TCP probe of Unity-MCP :8080 (informational only — no MCP RPC).
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

    # .meta 가 이번 커밋에 없어도 git 이 *이미 추적* 중이면(이전 커밋에서 등록) 정상이다.
    #
    # ★ 예전엔 「디스크에 있나」(Test-Path) 로 판정했다. 그러면 GUID 유실이 실제로 새는 경로를
    #   놓친다 — 에디터가 켜져 있으면 Unity 가 .meta 를 곧바로 만들어 두므로, .cs 만 커밋해도
    #   디스크 검사는 통과하고 카나리아는 조용하다. 다른 머신은 그 .meta 를 못 받아 새 GUID 를
    #   만들고, 같은 파일이 각자 다른 자산이 된다.
    #   (2026-08-05 실측: 한 밤에 두 번 났고 둘 다 무음. 추적 여부로 봐야 잡힌다.)
    & git ls-files --error-unmatch -- $expectedMeta 2>$null | Out-Null
    if ($LASTEXITCODE -ne 0)
    {
        $csMissingMeta += $path
    }
}

$bigCommit = $csCount -gt $BigCommitCsThreshold

# Unity-MCP TCP probe — fast (~300ms timeout). Informational only.
$mcpUp = $false
try
{
    $tcp = New-Object System.Net.Sockets.TcpClient
    $iar = $tcp.BeginConnect('127.0.0.1', 8080, $null, $null)
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
    Write-Host "[wm-verify]   info: Unity-MCP :8080 미응답 — 다음 commit 전 Editor Console / read_console 으로 컴파일 검증 권장."
}

exit 0
