# wm-init-order-audit.ps1 -- TASK-WM-115 init-order anti-pattern gate.
# Surfaces the anti-pattern codified in WitchMendokusai/CLAUDE.md
#   "Object reference acquisition - init-order safety".
#
# Flags (heuristic, line-based -- not a C# parser):
#   [BLOCK] Find(Any|Objects)ByType called inside Awake() or [Inject] Construct()
#           -> target may not exist yet (Awake < Start); use lazy Ensure / owner-push / [Inject].
#   [REVIEW] container.Inject( ... )  -> verify it should be InjectGameObject(go) for hierarchies.
#
# Suppress a justified line with a trailing comment:  // init-order-ok: <reason>
# 메서드 전체를 정당화하려면 같은 마커를 **메서드 시그니처 줄이나 바로 위 주석 블록**에 둔다.
#   // init-order-ok: 씬 정적 배치라 Start 시점 존재 보장
#   private void Start()
# (2026-08-08 실측: 이유를 메서드 위에 적어 둔 파일 2개가 계속 잡히고 있었다 — 마커가
#  같은 줄에만 걸려서. 사람은 이유를 붙였다고 믿고, 게이트는 영원히 빨간 줄을 3개 들고 있었다.)
#
# Exit 1 if any unsuppressed [BLOCK]. [REVIEW] is informational (exit unaffected).
#
# Usage:  powershell -File memo/dotfiles/scripts/wm-init-order-audit.ps1 [-Root <path>]

param(
    [string]$Root,
    # 표본으로 「게이트가 아직 눈을 뜨고 있는지」 자기 검사. 잡을 것을 잡고 면제할 것을 면제하는지 본다.
    # 이게 없던 동안 게이트는 못 보는 이름을 찾으며 몇 달간 초록이었다 (TASK-WM-211).
    [switch]$SelfTest
)

$ErrorActionPreference = "Stop"

if ($SelfTest)
{
    $fixtureRoot = Join-Path $PSScriptRoot "fixtures/init-order"
    if (-not (Test-Path $fixtureRoot))
    {
        Write-Output "[self-test] CANNOT-RUN: 표본 폴더가 없다: $fixtureRoot"
        exit 2
    }

    # CI 는 리눅스라 `powershell` 이라는 실행 파일이 없다 — 자기를 부르는 이름은 지금 도는 판에서 고른다.
    $psExe = if ($PSVersionTable.PSEdition -eq "Core") { "pwsh" } else { "powershell" }
    $out = & $psExe -NoProfile -File $PSCommandPath -Root $fixtureRoot 2>&1 | Out-String
    $expected = @{ "BLOCK" = 1; "ORDER-RISK" = 2; "AWAKE-CHAIN" = 1 }
    $failures = New-Object System.Collections.Generic.List[string]

    foreach ($kind in @("BLOCK", "ORDER-RISK", "AWAKE-CHAIN"))
    {
        $m = [regex]::Match($out, "\[$([regex]::Escape($kind))\][^\r\n]*?: (\d+)")
        $actual = if ($m.Success) { [int]$m.Groups[1].Value } else { -1 }
        if ($actual -ne $expected[$kind])
        {
            $failures.Add("[$kind] 기대 $($expected[$kind]) / 실제 $actual")
        }
    }

    Write-Output "=== wm-init-order-audit --SelfTest (TASK-WM-211) ==="
    Write-Output $out.Trim()
    Write-Output ""
    if ($failures.Count -gt 0)
    {
        Write-Output "SELF-TEST: FAIL -- 게이트가 표본을 예전처럼 못 본다."
        foreach ($x in $failures) { Write-Output ("  " + $x) }
        Write-Output "  표본을 일부러 바꿨다면 이 스크립트의 기대값도 같이 고쳐라."
        exit 1
    }
    Write-Output "SELF-TEST: PASS -- 잡을 것을 잡고, 면제할 것을 면제한다."
    exit 0
}

# 레포 폴더명을 하나 박아두면 그 이름이 아닌 순간 인자 없는 호출이 전부 죽는다.
# (실측 2026-08-05: 기본값이 `WitchMendokusai/...` 였는데 실제 폴더는 `Witch-Mendokusai`(하이픈)
#  → 인자 없이 부르면 항상 "Root not found". 자매 스크립트 wm-editmode-smoke.ps1 도 같은 병이었다.)
# 후보를 훑어 *실재하는* 것을 고른다. 못 찾으면 시도한 경로를 전부 찍는다 — 조용히 틀린 이유를 대지 않게.
$rootCandidates = New-Object System.Collections.Generic.List[string]
if ($Root)
{
    $rootCandidates.Add($Root)
}
else
{
    $umbrella = Split-Path $PSScriptRoot -Parent | Split-Path -Parent | Split-Path -Parent
    foreach ($repoName in @("Witch-Mendokusai", "WitchMendokusai"))
    {
        $rootCandidates.Add((Join-Path $repoName "Assets/_WitchMendokusai"))
        $rootCandidates.Add((Join-Path $umbrella (Join-Path $repoName "Assets/_WitchMendokusai")))
    }
}

$resolvedRoot = $null
foreach ($candidate in $rootCandidates)
{
    if (Test-Path $candidate) { $resolvedRoot = $candidate; break }
    # 상대 경로로 준 경우 umbrella 기준으로도 한 번 더 본다 (repo root / memo/ 어디서 불러도 되도록).
    $fromUmbrella = Join-Path (Split-Path $PSScriptRoot -Parent | Split-Path -Parent | Split-Path -Parent) $candidate
    if (Test-Path $fromUmbrella) { $resolvedRoot = $fromUmbrella; break }
}

if (-not $resolvedRoot)
{
    Write-Output "[init-order] ERROR: 스캔할 소스 폴더를 못 찾았다. 시도한 경로:"
    $rootCandidates | ForEach-Object { Write-Output "[init-order]        $_" }
    Write-Output "[init-order]        -Root <경로> 로 직접 지정할 것."
    exit 2
}
$Root = $resolvedRoot

$files = Get-ChildItem -Path $Root -Recurse -Filter *.cs -File

# 「0개 검사 = 통과」 방지. 위 경로 해석 가드가 *없는 폴더*는 이미 잡지만,
# **있는데 비어 있는 폴더**(예: 아직 체크아웃 안 된 새 worktree)는 통과해버린다.
# 그러면 초록불이 「위반 없음」이 아니라 「아무것도 안 봤음」을 뜻하게 된다.
if ($files.Count -eq 0)
{
    Write-Output "[init-order] CANNOT-RUN: $Root 아래 .cs 가 0개다."
    Write-Output "[init-order]   위반이 없는 게 아니라 아무것도 안 본 것이다 — 경로/체크아웃 확인."
    exit 2
}
$blocks = New-Object System.Collections.Generic.List[string]
$reviews = New-Object System.Collections.Generic.List[string]
$orderRisks = New-Object System.Collections.Generic.List[string]
# Awake/Construct 가 *부르는* 메서드 안의 Find. 메서드 이름만 보면 안 잡힌다 —
# 그런데 도는 시점은 Awake 와 같다(TASK-WM-212 실측: 창 묶음이 Awake→Init 로 씬을 훑고 있었다).
$awakeChains = New-Object System.Collections.Generic.List[string]

$methodSigRx = '(\bvoid\s+Awake\s*\(|\bpublic\s+void\s+Construct\s*\()'
$injectAttrRx = '^\s*\[Inject\]'
# 2026-08-08 실측: 옛 패턴 'Find(Any|Objects)ByType' 은 `FindAnyObjectByType` 을 **한 번도 못 봤다**
# (그 이름엔 Any 와 ByType 사이에 Object 가 있다). 룰이 1순위로 금지하는 바로 그 호출이 안 보였고,
# 게이트는 「위반 0」이 아니라 「그건 안 봤음」을 초록으로 보고하고 있었다. 이름을 통째로 적는다.
$findRx = '\bFind(AnyObjectByType|ObjectsByType|ObjectOfType|ObjectsOfType)\b'
$injectCallRx = '\bcontainer\.Inject\s*\('
$okRx = '//\s*init-order-ok'
# TASK-WM-118 — cross-ref-at-lifecycle 클래스 (마스킹체인 :51→:47→:74 의 메커니즘):
# Start/OnEnable/Init/OnInit 안에서 sibling 을 Find -> 무보장 순서 의존. [BLOCK](Awake/
# Construct = 확정 too-early) 와 구분된 [ORDER-RISK](검토 — Start-Find 가 항상 버그는
# 아니나 그 클래스이므로 가시화). 근본 = lazy/owner-push/scope 결정합성 (WM-118 I3 정합).
$riskSigRx = '(\bvoid\s+Start\s*\(|\bIEnumerator\s+Start\s*\(|\bvoid\s+OnEnable\s*\(|\b(override\s+)?void\s+(On)?Init\s*\()'

# 보고용 경로. 게임 소스는 `Assets\...` 로 잘라 읽기 좋게, 그 밖(표본 폴더 등)은 스캔 루트 기준 상대경로로.
# ("Assets" 를 무조건 찾던 옛 코드는 표본 검사에서 그대로 터졌다 — 못 찾으면 -1 을 잘라내려 든다.)
function Get-ReportPath([string]$fullPath, [string]$scanRoot)
{
    $marker = $fullPath.IndexOf("Assets")
    if ($marker -ge 0) { return $fullPath.Substring($marker) }
    $rootFull = (Resolve-Path -LiteralPath $scanRoot).Path.TrimEnd('\', '/')
    if ($fullPath.StartsWith($rootFull)) { return $fullPath.Substring($rootFull.Length).TrimStart('\', '/') }
    return $fullPath
}

# 어떤 줄이든, 그 줄 자체나 **바로 위에 붙은 주석 블록**에 마커가 있으면 정당화된 것으로 본다.
# (메서드 시그니처에 쓰면 그 메서드 전체 / 문장 위에 쓰면 그 문장 하나.)
# 사람이 이유를 쓰는 자리가 「바로 위 줄」이다 — 같은 줄에서만 찾으면 그 이유는 없는 셈이 된다.
# 코드 줄을 만나면 거슬러 올라가기를 멈추므로 옆 문장으로 새지 않는다.
function Test-MethodScopeOk([string[]]$lines, [int]$sigIndex, [string]$okRx)
{
    if ($lines[$sigIndex] -match $okRx) { return $true }
    for ($j = $sigIndex - 1; $j -ge 0; $j--)
    {
        $above = $lines[$j].Trim()
        if ($above -eq '') { continue }
        # 주석도 어트리뷰트도 아니면 그 위는 남의 코드다 — 블록 끝.
        if ($above -notmatch '^(//|/\*|\*|\[)') { break }
        if ($above -match $okRx) { return $true }
    }
    return $false
}

foreach ($f in $files) {
    $lines = Get-Content -LiteralPath $f.FullName
    $inMethod = $false       # inside Awake/Construct body
    $depth = 0
    $sawInjectAttr = $false
    $methodOk = $false       # 이 메서드 전체가 // init-order-ok 로 정당화됐나
    # Awake/Construct 가 인자 없이 부르는 같은 클래스 메서드 이름들 (한 단계만 따라간다).
    $awakeCallees = New-Object System.Collections.Generic.HashSet[string]
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]

        if ($line -match $injectAttrRx) { $sawInjectAttr = $true }

        if (-not $inMethod) {
            $isAwake = $line -match '\bvoid\s+Awake\s*\('
            $isConstruct = ($line -match '\bvoid\s+Construct\s*\(') -and $sawInjectAttr
            if ($isAwake -or $isConstruct) {
                $inMethod = $true
                $methodOk = Test-MethodScopeOk $lines $i $okRx
                $depth = 0
                $depth += ([regex]::Matches($line, '\{')).Count
                $depth -= ([regex]::Matches($line, '\}')).Count
                $sawInjectAttr = $false
                continue
            }
            if ($line.Trim() -ne '' -and $line -notmatch $injectAttrRx) { $sawInjectAttr = $false }
        }
        else {
            $depth += ([regex]::Matches($line, '\{')).Count
            $depth -= ([regex]::Matches($line, '\}')).Count
            if ($line -match $findRx -and (Test-MethodScopeOk $lines $i $okRx) -eq $false -and -not $methodOk) {
                # 주석 안의 언급은 호출이 아니다 — 「이 함수는 여기서 쓰면 안 된다」고 적어 둔 줄까지 잡으면
                # 게이트가 자기 설명서를 위반으로 신고한다(실측: DungeonManager 의 경고 주석).
                $cm = $line.IndexOf('//')
                $fm = [regex]::Match($line, $findRx).Index
                if (-not ($cm -ge 0 -and $cm -lt $fm)) {
                    $rel = Get-ReportPath $f.FullName $Root
                    $blocks.Add(("{0}:{1}: {2}" -f $rel, ($i + 1), $line.Trim()))
                }
            }

            # Awake 가 부르는 자기 메서드 이름 수집 — `Init();` 처럼 인자 없이 부르는 것만.
            # 한 단계면 충분하다: 실제로 걸린 모양(Awake → Init → 씬 훑기)이 딱 그 깊이였다.
            $callMatch = [regex]::Match($line, '^\s*([A-Za-z_]\w*)\s*\(\s*\)\s*;')
            if ($callMatch.Success) {
                $null = $awakeCallees.Add($callMatch.Groups[1].Value)
            }
            if ($depth -le 0) { $inMethod = $false }
        }
    }

    # Awake/Construct 가 부르는 메서드 안의 Find — 이름은 Init/Setup 이어도 **도는 시점은 Awake 다.**
    # (TASK-WM-212: 창 묶음이 Awake → Init 로 씬을 훑고 있었는데 등급이 한 칸 낮게 잡혔다.)
    if ($awakeCallees.Count -gt 0) {
        $inCallee = $false
        $cdepth = 0
        $calleeOk = $false
        $calleeName = ''
        for ($i = 0; $i -lt $lines.Count; $i++) {
            $line = $lines[$i]
            if (-not $inCallee) {
                $sig = [regex]::Match($line, '\b(?:void)\s+([A-Za-z_]\w*)\s*\(\s*\)')
                if ($sig.Success -and $awakeCallees.Contains($sig.Groups[1].Value)) {
                    $inCallee = $true
                    $calleeName = $sig.Groups[1].Value
                    $calleeOk = Test-MethodScopeOk $lines $i $okRx
                    $cdepth = ([regex]::Matches($line, '\{')).Count - ([regex]::Matches($line, '\}')).Count
                    continue
                }
            }
            else {
                $cdepth += ([regex]::Matches($line, '\{')).Count
                $cdepth -= ([regex]::Matches($line, '\}')).Count
                if ($line -match $findRx -and (Test-MethodScopeOk $lines $i $okRx) -eq $false -and -not $calleeOk) {
                    $cm3 = $line.IndexOf('//')
                    $fm3 = [regex]::Match($line, $findRx).Index
                    if (-not ($cm3 -ge 0 -and $cm3 -lt $fm3)) {
                        $rel = Get-ReportPath $f.FullName $Root
                        $awakeChains.Add(("{0}:{1}: [Awake -> {2}] {3}" -f $rel, ($i + 1), $calleeName, $line.Trim()))
                    }
                }
                if ($cdepth -le 0) { $inCallee = $false }
            }
        }
    }

    # informational: container.Inject( occurrences (InjectGameObject review)
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $ln = $lines[$i]
        if ($ln -notmatch $injectCallRx) { continue }
        if ($ln -match 'InjectGameObject' -or $ln -match $okRx) { continue }
        # skip when the match is inside a // comment (comment starts before container.Inject)
        $cm = $ln.IndexOf('//')
        $cl = $ln.IndexOf('container.Inject')
        if ($cm -ge 0 -and $cm -lt $cl) { continue }
        $rel = Get-ReportPath $f.FullName $Root
        $reviews.Add(("{0}:{1}: {2}" -f $rel, ($i + 1), $ln.Trim()))
    }

    # TASK-WM-118: Find*ObjectByType inside Start/OnEnable/Init/OnInit = [ORDER-RISK]
    $inRisk = $false
    $rdepth = 0
    $riskOk = $false
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        if (-not $inRisk) {
            if ($line -match $riskSigRx) {
                $inRisk = $true
                $riskOk = Test-MethodScopeOk $lines $i $okRx
                $rdepth = ([regex]::Matches($line, '\{')).Count - ([regex]::Matches($line, '\}')).Count
                continue
            }
        }
        else {
            $rdepth += ([regex]::Matches($line, '\{')).Count
            $rdepth -= ([regex]::Matches($line, '\}')).Count
            if ($line -match $findRx -and (Test-MethodScopeOk $lines $i $okRx) -eq $false -and -not $riskOk) {
                $cm2 = $line.IndexOf('//')
                $fm2 = [regex]::Match($line, $findRx).Index
                if (-not ($cm2 -ge 0 -and $cm2 -lt $fm2)) {
                    $rel = Get-ReportPath $f.FullName $Root
                    $orderRisks.Add(("{0}:{1}: {2}" -f $rel, ($i + 1), $line.Trim()))
                }
            }
            if ($rdepth -le 0) { $inRisk = $false }
        }
    }
}

Write-Output "=== wm-init-order-audit (TASK-WM-115 gate) ==="
Write-Output ("scanned .cs files: {0}" -f $files.Count)
Write-Output ""
Write-Output ("[BLOCK] Find*ObjectByType inside Awake/Construct : {0}" -f $blocks.Count)
foreach ($b in $blocks) { Write-Output ("  " + $b) }
Write-Output ""
Write-Output ("[REVIEW] container.Inject( ... ) -> verify InjectGameObject : {0}" -f $reviews.Count)
foreach ($r in $reviews) { Write-Output ("  " + $r) }
Write-Output ""
Write-Output ("[ORDER-RISK] Find*ObjectByType in Start/OnEnable/Init/OnInit (cross-ref-at-lifecycle, WM-118) : {0}" -f $orderRisks.Count)
foreach ($o in $orderRisks) { Write-Output ("  " + $o) }
Write-Output ""
Write-Output ("[AWAKE-CHAIN] Awake/Construct 가 부르는 메서드 안의 Find (이름은 Init 이어도 시점은 Awake, WM-212) : {0}" -f $awakeChains.Count)
foreach ($c in $awakeChains) { Write-Output ("  " + $c) }
Write-Output ""

if ($blocks.Count -gt 0) {
    Write-Output ("RESULT: FAIL -- {0} unsuppressed [BLOCK]. Fix at root (lazy Ensure / owner-push / [Inject]) or annotate '// init-order-ok: <reason>'." -f $blocks.Count)
    exit 1
}
Write-Output "RESULT: PASS -- 0 [BLOCK]. ([REVIEW] is informational.)"
exit 0
