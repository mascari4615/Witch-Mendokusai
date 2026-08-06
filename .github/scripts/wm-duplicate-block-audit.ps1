# wm-duplicate-block-audit.ps1 -- 같은 파일 안에 **똑같은 덩어리가 두 번** 들어간 것을 찾는다.
#
# WHY (2026-08-06 실측): `TowerDefenseMatch.cs` 에 38줄짜리 블록이 통째로 두 번 들어가
# `origin/main` 이 컴파일 RED 가 됐고(CS0111 x3), **그 위에 커밋이 하나 더 쌓일 때까지
# 아무 신호도 없었다.** 이유는 단순하다 — `wm-quality-gate.yml` 에서 C# 을 실제 컴파일하는
# 잡이 `push`/`pull_request` 에서 꺼져 있어(`if: github.event_name != 'push' && ...`),
# push 경로에는 **컴파일 그물이 0개**다.
#
# 이 검사는 그 구멍을 유니티 없이 메우는 값싼 그물이다. 컴파일러를 대신하진 못하지만,
# 오늘 난 사고의 *모양*(붙여넣기 중복)은 정확히 잡는다. ubuntu 러너에서 몇 초면 끝난다.
#
# ★ 왜 「메서드 시그니처 중복」이 아니라 「연속 동일 줄」인가:
#   시그니처 매칭은 오버로드 / partial class / 인터페이스 구현에서 **거짓 양성**이 난다.
#   거짓 경고는 게이트를 죽인다(이 레포 원장의 반복 교훈). 반면 「의미 있는 코드 20줄이
#   한 파일 안에서 토씨 하나 안 틀리고 두 번」은 정상 코드에선 사실상 안 나온다.
#
# Exit: 0 = 중복 없음 / 1 = 중복 발견 / 2 = 검사 자체가 못 돌았음(대상 0개 등)
#
# Usage:
#   pwsh -File .github/scripts/wm-duplicate-block-audit.ps1 [-Root <path>] [-MinLines 20]

# ★ 임계값은 눈대중이 아니라 **실측으로** 골랐다 (2026-08-06, .cs 1131개 전수):
#     MinLines=20 → 2건 (진짜 1 + **거짓 1**)
#     MinLines=25 → 1건 (진짜만)
#     MinLines=30 → 1건 (진짜만)
#     MinLines=35 → 1건 (진짜만)
#   거짓 1건 = `Motor.cs` 의 `떨어지는 캐릭터` / `올라가는 캐릭터` 두 메서드다. 둘 다
#   「발/머리 중심 raycast → 가장 가까운 hit 찾기」라 20줄쯤이 자연히 닮는다 — **정상 코드다.**
#   그래서 그 길이(~24줄) 위로 여유를 둔 **30** 을 기본값으로 잡았다.
#   25 도 오늘 기준 거짓 0 이었으니, 더 민감하게 가고 싶으면 근거를 알고 낮추면 된다.
param(
    [string]$Root = "Assets/_WitchMendokusai",
    [int]$MinLines = 30
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $Root))
{
    Write-Host "wm-duplicate-block -- CANNOT-RUN: 경로가 없다: $Root"
    Write-Host "wm-duplicate-block --   위반이 없는 게 아니라 아무것도 안 본 것이다."
    exit 2
}

$files = @(Get-ChildItem -Path $Root -Filter *.cs -Recurse -File)

# 「대상 0건 = 통과」 방지 — 경로가 어긋나면 조용히 초록이 뜬다.
if ($files.Count -eq 0)
{
    Write-Host "wm-duplicate-block -- CANNOT-RUN: $Root 아래 .cs 가 0개다."
    Write-Host "wm-duplicate-block --   위반이 없는 게 아니라 아무것도 안 본 것이다."
    exit 2
}

Write-Host "wm-duplicate-block -- scanned $($files.Count) .cs files under $Root (min $MinLines lines)"

# 의미 없는 줄(빈 줄 / 닫는 괄호 / 여는 괄호만)은 창(window)의 실질 내용으로 안 센다.
# 이걸 안 하면 `}` 만 20줄 이어진 구간이 서로 매칭돼 거짓 양성이 쏟아진다.
function Test-TrivialLine([string]$line)
{
    $t = $line.Trim()
    if ($t.Length -eq 0) { return $true }
    if ($t -eq "{" -or $t -eq "}" -or $t -eq "};") { return $true }
    if ($t.StartsWith("//")) { return $true }
    return $false
}

$offenders = @()

foreach ($file in $files)
{
    $lines = [System.IO.File]::ReadAllLines($file.FullName)
    if ($lines.Length -lt ($MinLines * 2)) { continue }

    $seen = @{}
    for ($i = 0; $i -le ($lines.Length - $MinLines); $i++)
    {
        $window = $lines[$i..($i + $MinLines - 1)]

        # 실질 줄이 절반 미만이면 건너뛴다 — 괄호/빈줄 덩어리는 판정 대상이 아니다.
        $substantive = 0
        foreach ($w in $window) { if (-not (Test-TrivialLine $w)) { $substantive++ } }
        if ($substantive -lt [Math]::Ceiling($MinLines / 2)) { continue }

        $key = ($window | ForEach-Object { $_.Trim() }) -join "`n"
        if ($seen.ContainsKey($key))
        {
            $offenders += [pscustomobject]@{
                File  = $file.FullName.Substring((Resolve-Path $Root).Path.Length).TrimStart('\', '/')
                First = $seen[$key] + 1
                Again = $i + 1
            }
            break   # 파일당 첫 건만 보고 — 중복은 대개 한 덩어리라 목록이 길어질 필요 없다.
        }
        $seen[$key] = $i
    }
}

if ($offenders.Count -eq 0)
{
    Write-Host "wm-duplicate-block -- RESULT: PASS (동일 블록 중복 0건)"
    exit 0
}

Write-Host ""
Write-Host "wm-duplicate-block -- 같은 파일 안에 동일한 $MinLines+ 줄 덩어리가 두 번 나온다:"
foreach ($o in $offenders)
{
    Write-Host ("  {0}  --  {1}행 과 {2}행" -f $o.File, $o.First, $o.Again)
}
Write-Host ""
Write-Host "wm-duplicate-block --   붙여넣기가 두 번 들어갔을 가능성이 높다."
Write-Host "wm-duplicate-block --   메서드가 겹치면 CS0111 로 컴파일이 죽는다(2026-08-06 실제 사고)."
Write-Host "wm-duplicate-block --   지우기 전에 두 덩어리의 **정확한 경계**를 diff 로 확인할 것 --"
Write-Host "wm-duplicate-block --   길이를 눈대중하면 메서드 중간이 잘린다(그 실수도 같은 날 났다)."
Write-Host "wm-duplicate-block -- RESULT: FAIL"
exit 1
