# Tools/git-hooks/wm-commit-verify.ps1 — TASK-WM-109-F
#
# Lightweight post-commit verification for WitchMendokusai.
#
# Responsibilities:
#   1. Append commit metadata row to <git-common-dir>/wm-commit-log.tsv (ledger).
#   2. Count touched .cs / .meta / .asset / .prefab / .unity files in the commit.
#   3. Pair-check .cs (added/modified) ↔ .cs.meta — Unity GUID-loss canary.
#   4. Heuristic "big commit" advisory when .cs count exceeds a small threshold.
#
# Non-responsibilities:
#   - Calling Unity (compile / Play). Hooks must be fast; canonical compile
#     verification stays wm-compile-check + official Unity CLI console.
#   - Blocking commits — this is post-commit; runs always with exit 0.
#
# Invoked by .git/hooks/post-commit after install.ps1 sets it up.

[CmdletBinding()]
param(
    [string]$Sha = $env:WM_COMMIT_SHA,
    [int]$BigCommitCsThreshold = 10
)

$ErrorActionPreference = 'Continue'

# 콘솔 출력 인코딩 고정. 이걸 안 하면 PowerShell 이 콘솔 코드페이지(cp949)로 내보내
# git 훅 경유(Git Bash·CI 로그)에서 한글이 깨진다 — 실측. 경고 문구가 안 읽히면
# 경고가 없는 것과 같다. try 로 감싼 이유는 리다이렉트된 스트림엔 콘솔이 없어서다.
try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }
$OutputEncoding = [System.Text.Encoding]::UTF8

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
    # core.quotepath=false — 한글 경로를 "\352\260..." 로 이스케이프하지 않고 raw UTF-8 로.
    # (켜져 있으면 아래 확장자 정규식이 따옴표 때문에 한글 경로를 통째로 놓친다.)
    $rawOutput = git -c core.quotepath=false show --name-status --format= $Sha 2>$null
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
#
# ★ 2026-08-06 실측 — 이 검사는 있었는데 **한 번도 안 울렸다.** 옛 판은 `Test-Path` 로
#   *디스크에* meta 가 있나만 봤다. 그런데 실패 모양이 바로 그것이다: 유니티가 meta 를
#   만들어 두면 디스크엔 있다. 다만 **커밋이 안 됐을 뿐이다.** 그래서 검사는 늘 통과했고
#   .cs 3개 + 폴더 1개가 meta 없이 main 에 올라갔다(945721d9 / 1694d3a1).
#   물어야 할 것은 「디스크에 있나」가 아니라 **「이 커밋 트리에 있나」** 다.
function Test-InCommitTree
{
    param([string]$CommitSha, [string]$RelPath)

    # 따옴표로 감싸져 온 경로(비ASCII를 git 이 이스케이프한 것)는 판단을 포기한다 —
    # 거짓 경고를 내느니 침묵이 낫다. quotepath=false 로 대부분 raw 로 온다.
    if ($RelPath.StartsWith('"')) { return $true }

    git cat-file -e "${CommitSha}:${RelPath}" 2>$null
    return ($LASTEXITCODE -eq 0)
}

$csMissingMeta = @()
$dirMissingMeta = @()
$checkedDirs = @{}

# 유니티가 짝(.meta)을 만들어 주는 곳은 `Assets/` 와 `Packages/` 뿐이다.
# 그 밖의 .cs — 빌드 도구, 게이트 표본, 하네스 스텁 — 은 짝이 애초에 없다.
# 그런 줄에 매번 「짝이 없다」고 외치면 진짜 GUID 사고가 났을 때 아무도 안 본다.
# (2026-08-08 실측: 하네스 스텁·게이트 표본 커밋에서 이 경고가 그냥 울렸다.)
function Test-UnityManagedPath
{
    param([string]$RelPath)
    return ($RelPath -match '^"?(Assets|Packages)/')
}

foreach ($line in $cs)
{
    $parts = $line -split "`t", 2
    if ($parts.Count -lt 2) { continue }
    $status = $parts[0]
    $path = $parts[1]
    if ($status -notmatch '^[AM]') { continue }
    if (-not (Test-UnityManagedPath $path)) { continue }

    if (-not (Test-InCommitTree $Sha "$path.meta"))
    {
        $csMissingMeta += $path
    }
}

# 폴더도 meta 를 갖는다. 새 폴더에 .cs 를 넣으면 폴더 meta 가 같이 안 올라가기 쉽다
# (Tests/EditMode/Diagnostics 가 그랬다). 추가(A)된 파일의 조상 폴더만 본다 —
# 이미 있던 폴더는 어차피 meta 가 있고, 중복은 캐시로 한 번씩만 묻는다.
foreach ($line in $cs)
{
    $parts = $line -split "`t", 2
    if ($parts.Count -lt 2) { continue }
    if ($parts[0] -notmatch '^A') { continue }

    $segments = $parts[1] -split '/'
    for ($i = 1; $i -lt $segments.Count; $i++)
    {
        $dir = ($segments[0..($i - 1)] -join '/')
        if ($dir -eq 'Assets' -or -not $dir.StartsWith('Assets/')) { continue }
        if ($checkedDirs.ContainsKey($dir)) { continue }
        $checkedDirs[$dir] = $true

        if (-not (Test-InCommitTree $Sha "$dir.meta"))
        {
            $dirMissingMeta += $dir
        }
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
        # ★ 순수 주석 줄은 뺀다 (실측 2026-08-06: 이 canary 가 **나를** 잡았다 — 개명 이유를 설명하는
        #   `/// ArenaCombatant → MatchCombatant 개명` 한 줄에 걸렸다).
        #   이 검사가 막으려는 사고는 「옛 사본이 옛 *타입* 을 되살려 main 이 CS0246 으로 죽는 것」이다.
        #   주석은 컴파일에 영향이 없으므로 그 사고를 만들 수 없다 = 신호가 아니라 잡음이다.
        #   단 **코드 + 꼬리주석** 줄(`Foo x; // 옛날엔 Bar`)은 그대로 본다 — 앞부분이 진짜 코드라서다.
        #   그래서 「trim 했을 때 주석으로 시작하는 줄」만 건너뛴다(과보정으로 진짜를 놓치지 않게).
        $addedLines = @()
        foreach ($entry in @(git show --unified=0 --format= $Sha -- '*.cs' 2>$null))
        {
            foreach ($line in ($entry -split "`n"))
            {
                if (-not $line.StartsWith('+')) { continue }
                if ($line.StartsWith('+++')) { continue }

                $code = $line.Substring(1).TrimStart()
                if ($code.StartsWith('//') -or $code.StartsWith('*') -or $code.StartsWith('/*')) { continue }

                $addedLines += $line
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

# Ledger — TSV, header on first write.
$ledger = Join-Path $paths.CommonDir 'wm-commit-log-v2.tsv'
$header = "ts`tsha`tauthor`tcs`tmeta`tasset`tprefab`tscene`tparents`tsubject"
if (-not (Test-Path -LiteralPath $ledger))
{
    Set-Content -LiteralPath $ledger -Value $header -Encoding UTF8
}
$cleanSubject = ($subject -replace "`t", ' ').Trim()
$row = "$tsIso`t$shortSha`t$author`t$csCount`t$metaCount`t$assetCount`t$prefabCount`t$sceneCount`t$parentCount`t$cleanSubject"
Add-Content -LiteralPath $ledger -Value $row -Encoding UTF8

# Console summary — single line headline + optional advisories.
$tag = if ($parentCount -gt 1) { '[merge]' } elseif ($bigCommit) { '[big]' } else { '[ok]' }
Write-Host "[wm-verify] $tag $shortSha  cs=$csCount meta=$metaCount asset=$assetCount prefab=$prefabCount scene=$sceneCount"

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

if ($csMissingMeta.Count -gt 0 -or $dirMissingMeta.Count -gt 0)
{
    Write-Host "[wm-verify]   ★ .meta 가 커밋에 없다 — 디스크엔 있을 수 있다(그게 함정이다):"
    foreach ($p in ($csMissingMeta | Select-Object -First 5))
    {
        Write-Host "[wm-verify]     - $p.meta"
    }
    if ($csMissingMeta.Count -gt 5)
    {
        Write-Host "[wm-verify]     ... +$($csMissingMeta.Count - 5) more"
    }
    foreach ($d in ($dirMissingMeta | Select-Object -First 5))
    {
        Write-Host "[wm-verify]     - $d.meta  (폴더)"
    }
    if ($dirMissingMeta.Count -gt 5)
    {
        Write-Host "[wm-verify]     ... +$($dirMissingMeta.Count - 5) more (폴더)"
    }
    Write-Host "[wm-verify]     고침: git add <위 경로들> && git commit --amend --no-edit"
    Write-Host "[wm-verify]     왜: GUID 가 머신마다 달라진다 → 나중에 프리팹이 참조하는 순간"
    Write-Host "[wm-verify]         **다른 머신에서만** MissingScript. 작업트리도 영영 더럽다."
}

exit 0
