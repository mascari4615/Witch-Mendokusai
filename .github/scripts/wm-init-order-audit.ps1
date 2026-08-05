# wm-init-order-audit.ps1 -- TASK-WM-115 init-order anti-pattern gate.
# Surfaces the anti-pattern codified in WitchMendokusai/CLAUDE.md
#   "Object reference acquisition - init-order safety".
#
# Flags (heuristic, line-based -- not a C# parser):
#   [BLOCK] Find(Any|Objects)ByType called inside Awake() or [Inject] Construct()
#           -> target may not exist yet (Awake < Start); use lazy Ensure / owner-push / [Inject].
#   [REVIEW] container.Inject( ... )  -> verify it should be InjectGameObject(go) for hierarchies.
#
# Suppress a justified single line with a trailing comment:  // init-order-ok: <reason>
#
# Exit 1 if any unsuppressed [BLOCK]. [REVIEW] is informational (exit unaffected).
#
# Usage:  powershell -File memo/dotfiles/scripts/wm-init-order-audit.ps1 [-Root <path>]

param(
    [string]$Root
)

$ErrorActionPreference = "Stop"

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
$blocks = New-Object System.Collections.Generic.List[string]
$reviews = New-Object System.Collections.Generic.List[string]
$orderRisks = New-Object System.Collections.Generic.List[string]

$methodSigRx = '(\bvoid\s+Awake\s*\(|\bpublic\s+void\s+Construct\s*\()'
$injectAttrRx = '^\s*\[Inject\]'
$findRx = 'Find(Any|Objects)ByType'
$injectCallRx = '\bcontainer\.Inject\s*\('
$okRx = '//\s*init-order-ok'
# TASK-WM-118 — cross-ref-at-lifecycle 클래스 (마스킹체인 :51→:47→:74 의 메커니즘):
# Start/OnEnable/Init/OnInit 안에서 sibling 을 Find -> 무보장 순서 의존. [BLOCK](Awake/
# Construct = 확정 too-early) 와 구분된 [ORDER-RISK](검토 — Start-Find 가 항상 버그는
# 아니나 그 클래스이므로 가시화). 근본 = lazy/owner-push/scope 결정합성 (WM-118 I3 정합).
$riskSigRx = '(\bvoid\s+Start\s*\(|\bIEnumerator\s+Start\s*\(|\bvoid\s+OnEnable\s*\(|\b(override\s+)?void\s+(On)?Init\s*\()'

foreach ($f in $files) {
    $lines = Get-Content -LiteralPath $f.FullName
    $inMethod = $false       # inside Awake/Construct body
    $depth = 0
    $sawInjectAttr = $false
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]

        if ($line -match $injectAttrRx) { $sawInjectAttr = $true }

        if (-not $inMethod) {
            $isAwake = $line -match '\bvoid\s+Awake\s*\('
            $isConstruct = ($line -match '\bvoid\s+Construct\s*\(') -and $sawInjectAttr
            if ($isAwake -or $isConstruct) {
                $inMethod = $true
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
            if ($line -match $findRx -and $line -notmatch $okRx) {
                $rel = $f.FullName.Substring($f.FullName.IndexOf("Assets"))
                $blocks.Add(("{0}:{1}: {2}" -f $rel, ($i + 1), $line.Trim()))
            }
            if ($depth -le 0) { $inMethod = $false }
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
        $rel = $f.FullName.Substring($f.FullName.IndexOf("Assets"))
        $reviews.Add(("{0}:{1}: {2}" -f $rel, ($i + 1), $ln.Trim()))
    }

    # TASK-WM-118: Find*ObjectByType inside Start/OnEnable/Init/OnInit = [ORDER-RISK]
    $inRisk = $false
    $rdepth = 0
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        if (-not $inRisk) {
            if ($line -match $riskSigRx) {
                $inRisk = $true
                $rdepth = ([regex]::Matches($line, '\{')).Count - ([regex]::Matches($line, '\}')).Count
                continue
            }
        }
        else {
            $rdepth += ([regex]::Matches($line, '\{')).Count
            $rdepth -= ([regex]::Matches($line, '\}')).Count
            if ($line -match $findRx -and $line -notmatch $okRx) {
                $cm2 = $line.IndexOf('//')
                $fm2 = [regex]::Match($line, $findRx).Index
                if (-not ($cm2 -ge 0 -and $cm2 -lt $fm2)) {
                    $rel = $f.FullName.Substring($f.FullName.IndexOf("Assets"))
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

if ($blocks.Count -gt 0) {
    Write-Output ("RESULT: FAIL -- {0} unsuppressed [BLOCK]. Fix at root (lazy Ensure / owner-push / [Inject]) or annotate '// init-order-ok: <reason>'." -f $blocks.Count)
    exit 1
}
Write-Output "RESULT: PASS -- 0 [BLOCK]. ([REVIEW] is informational.)"
exit 0
