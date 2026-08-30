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
    [string[]]$Paths,
    # Same as -Paths, but read from a file (one path per line).
    #
    # WHY (2026-08-13): a new-branch push carries *every* .cs in the commit (1354 files
    # here). Passing that list as command-line arguments kills the call itself with
    # "Argument list too long" -- and the caller printed "fix your rule violations",
    # which is a lie: the gate never ran. A file has no such ceiling.
    [string]$PathsFrom,
    # 기준선을 **게이트 자신이** 써 낸다. 손으로 짜거나 별도 스크립트로 뽑으면 판정과
    # 목록이 어긋나 「기준선에 있는데도 빨강」이 난다. 빚을 갚은 뒤 갱신할 때도 이걸 쓴다.
    [switch]$WriteBaseline
)

$ErrorActionPreference = 'Stop'

# ★ git 출력은 UTF-8 로 받는다 (2026-08-13, TASK-WM-326).
#   PowerShell 5.1 은 외부 명령의 stdout 을 <콘솔 코드페이지>(여기선 949)로 디코드한다.
#   한글 주석이 든 .cs 를 그렇게 읽으면 UTF-8 바이트가 이중바이트로 잘못 묶이면서
#   **뒤따르는 줄바꿈(0x0A)이 통째로 먹힌다** -- 그러면 `// 주석` 이 다음 줄까지 삼켜서
#   `Mine = 3,` `Craft = 5,` 와 enum 의 닫는 괄호가 텍스트에서 사라진다.
#   결과는 있지도 않은 룰 위반이다: BootPhase/WorkKind 는 값을 다 적어 뒀는데도
#   push 때만 ENUM-VALUE 빨강이 났다(작업 트리로 읽는 전수 검사는 초록 -- 같은 코드, 다른 판정).
#   검사기가 <파일을 잘못 읽어> 내는 빨강은 제품 소식이 아니다.
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)

if (-not [string]::IsNullOrWhiteSpace($PathsFrom))
{
    if (-not (Test-Path -LiteralPath $PathsFrom))
    {
        Write-Host "wm-rule-gate -- CANNOT-RUN: -PathsFrom file not found: $PathsFrom"
        exit 1
    }

    $Paths = @(Get-Content -LiteralPath $PathsFrom -Encoding UTF8 | Where-Object { $_.Trim().Length -gt 0 })
}

$commitScoped = -not [string]::IsNullOrWhiteSpace($Sha)

# A commit-scoped run that was *given* a list but ended up with none is the "gate did not
# run" case again (bad file, wrong encoding) -- say so instead of exiting 0.
if ($commitScoped -and -not [string]::IsNullOrWhiteSpace($PathsFrom) -and $Paths.Count -eq 0)
{
    Write-Host "wm-rule-gate -- CANNOT-RUN: -PathsFrom listed 0 paths: $PathsFrom"
    exit 1
}

if (-not $commitScoped)
{
    if ([string]::IsNullOrWhiteSpace($Root))
    {
        $repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
        $Root = Join-Path $repoRoot 'Assets/_WitchMendokusai'
    }

    if (-not (Test-Path -LiteralPath $Root))
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
        # .NET Standard **2.1** 에만 있는 API 다. 이 프로젝트의 API 수준은 2.0
        # (`ProjectSettings.asset: apiCompatibilityLevel: 6`) 이라 에디터에서는 넘어가도
        # **플레이어 빌드에서 컴파일이 깨진다.**
        #
        # 실제 사고(2026-08-09~13): `Environment.TickCount64` 한 줄 때문에 야간 윈도우 빌드가
        # 나흘간 죽었고, 그 빌드를 먹는 런타임 관문(부팅·2인 동기)도 같이 빨갛게 멈춰 있었다.
        # 빌드는 밤에만 도니까 아무도 그날 못 알아챘다 -- push 때 여기서 세운다.
        #
        # 목록은 **추측이 아니라 실측**이다: netstandard2.0 으로 실제 컴파일해서 없는 것만 넣었다.
        # (`string.Contains(char)` 은 넣으려다 빼었다 -- 2.0 에서도 컴파일된다.)
        # 새 항목을 넣을 땐 같은 방법으로 확인할 것: 빈 netstandard2.0 프로젝트에 한 줄 써 보기.
        Id      = 'NETSTD21-API'
        Title   = '.NET Standard 2.1 API is banned -- this project is pinned to 2.0'
        Fix     = 'use a 2.0 equivalent (e.g. DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() for TickCount64, Mathf for MathF)'
        Strings = $false
        Match   = {
            param($line)
            # ★ `-cmatch` (대소문자 구분) 필수. `-match` 는 무시하므로 Unity 의 **정상**
            #   `Mathf.` 606곳을 `MathF.` 로 오인해 전부 빨갛게 만들었다(실측).
            # ★ `System.` 을 붙여 쓴 꼴도 잡아야 한다. 앞 문자 배제만 쓰면 `MyEnvironment.` 는
            #   막으면서 **정작 사고를 낸 `System.Environment.TickCount64` 를 놓친다**(실측:
            #   일부러 그 줄을 넣었는데 안 걸렸다).
            return ($line -cmatch '(^|[^\w.])(System\.)?Environment\.TickCount64') -or
                   ($line -cmatch '(^|[^\w.])MathF\.') -or
                   ($line -cmatch '(^|[^\w.])HashCode\.Combine') -or
                   ($line -cmatch 'new\s+HashCode\s*\(') -or
                   ($line -cmatch '\.TryAdd\s*\(') -or
                   ($line -cmatch '\.ToHashSet\s*\(') -or
                   ($line -cmatch "\.Split\s*\(\s*'.'\s*,\s*(System\.)?StringSplitOptions")
        }
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
    },
    @{
        Id      = 'MENU-ASCII'
        Title   = 'Editor menu path must be ASCII only (no Korean). Rule: memo/rules/unity.md'
        Fix     = 'write the MenuItem path in English; window text, logs and tooltips may stay Korean'
        Strings = $true
        Match   = {
            param($line)
            if ($line -notmatch 'MenuItem\s*\(\s*"([^"]+)"') { return $false }
            $path = $Matches[1]
            return ($path -match '[^ -~]')
        }
    },
    @{
        Id      = 'UI-TEXT'
        Title   = 'Idle screen text must be a plain verb or value. Rule: memo/wm/design/idle/ui-system.md (UI text)'
        Fix     = 'button = one verb, no parenthesis explanation, no internal words (grade/zone/fragment/share/axis/core)'
        Strings = $true
        Match   = {
            param($line)
            if ($line -notmatch '\.text\s*=\s*"([^"]*)"') { return $false }
            $text = $Matches[1]
            if ($text -match '\(') { return $true }
            return ($text -match '(등급|구역|조각|보유 \+|축|코어|다음 조각|지금은|판 전체)')
        }
    }
)

# Comments are not code. An earlier hand-grep flagged commented-out `var` lines as
# violations -- a gate that cries wolf gets switched off, so strip them first.
function Write-BaselineFile
{
    # 기준선은 <b>LF</b> 로 적는다.
    #
    # ⚠ .NET 의 WriteAllLines 는 Environment.NewLine(윈도우에서 CRLF)을 쓴다. 저장소에는
    #   LF 로 담기므로, 한 번 새로 쓰면 그 뒤로 `git status` 가 <b>내용이 같은데도</b>
    #   「고쳐짐」으로 띄운다. 그러면 다음 세션이 그걸 <b>남이 안 담은 줄</b>로 읽는다 —
    #   이 저장소가 공유 작업면에서 제일 조심하는 그 오해다(실측 2026-08-17).
    param([string]$Path, [string[]]$Lines)

    [System.IO.File]::WriteAllText($Path, (($Lines -join "`n") + "`n"),
        (New-Object System.Text.UTF8Encoding($false)))
}

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
        $blob = & git show "${Sha}:${path}" 2>$null   # UTF-8 로 디코드된다 (위 참조)
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

# "0 files scanned" is NOT a pass -- it is the gate not running at all.
#
# WHY (2026-08-06): this gate reported `PASS -- 0 rule violations` purely from the
# count of hits, with nothing asserting it had actually looked at anything. Point
# -Root at a path that does not exist in this checkout and every rule "passes" and
# it exits 0. That is not hypothetical: sibling tooling in memo/scripts hardcoded
# the WM repo folder as `WitchMendokusai` while the real folder is
# `Witch-Mendokusai`, and three cleanup scripts silently skipped the whole repo
# for months while reporting success.
#
# Only the repo-scan mode gets this floor. In commit-scoped mode (-Sha/-Paths) an
# empty subject list is legitimate and common -- a commit that touches only assets,
# scenes or docs has no .cs to judge, and failing that would make the gate a liar
# in the opposite direction.
if (-not $commitScoped -and $subjects.Count -eq 0)
{
    Write-Host "wm-rule-gate -- CANNOT-RUN: found 0 .cs files under $Root"
    Write-Host "wm-rule-gate --   This is not 'no violations', it is 'nothing was examined'."
    Write-Host "wm-rule-gate --   Check the -Root path (the WM repo folder is 'Witch-Mendokusai')."
    exit 2
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
    # 2026-08-07: this exact line vanished for a day in a stale-copy commit. The arena
    # then handed units back to the pool with their brains still switched off, so the
    # SAME instance came out of the next dungeon unable to move. Compile green, tests
    # green, silent -- the failure shape this list exists for.
    @{ File = 'Domain/Arena/Match/ArenaMatch.cs'
       Needle = 'CombatUnitSpawner.RestoreBrains'
       Why = 'units go back to the pool with their brains off -- they stop moving in the dungeon afterwards' },
    @{ File = 'Domain/Arena/Match/ArenaMatch.cs'
       Needle = 'CombatUnitSpawner.RestoreAutoCast'
       Why = 'units go back to the pool with autocast off -- they never use skills again' },
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
       Why = 'hero moves by raw transform again -- stutters, walks through walls, pushes monsters' },
    # Draft cards that were offered but wired to nothing (2026-08-06). The card text promised an
    # effect and the run changed nothing -- it silently burns one of the player's picks.
    @{ File = 'Domain/TowerDefense/TowerDefenseMatch.cs'
       Needle = 'boons.EssenceMultiplier'
       Why = 'the essence-gain card does nothing again' },
    @{ File = 'Domain/TowerDefense/TowerDefenseMatch.cs'
       Needle = 'boons.NestDamageMultiplier'
       Why = 'the nest-damage card does nothing again' },
    @{ File = 'Domain/TowerDefense/TowerDefenseMatch.cs'
       Needle = 'cost * boons.ResearchCostMultiplier'
       Why = 'the research-discount card does nothing again' },
    # NOTE: a needle must match the CALL SITE, not a bare name -- a bare name also appears in the
# method's own definition, so deleting every call still "passes". Verified by deleting a call and
# watching this gate stay green (2026-08-06). A gate that lies is worse than no gate.
#
# The rest of what a wide sweeping commit erased on 2026-08-06. Each of these is a single call
    # whose removal compiles fine, passes every test, and kills a feature the user explicitly asked for.
    @{ File = 'Domain/TowerDefense/TowerDefenseHudView.cs'
       Needle = 'ResearchPanelRequested()'
       Why = 'the research button stops opening the constellation -- the whole research screen becomes unreachable' },
    @{ File = 'Domain/TowerDefense/TowerDefenseMatch.cs'
       Needle = 'AddApproachRing(mapLayout.CoreCell)'
       Why = 'monsters converge on one cell again instead of surrounding the core' },
    @{ File = 'DomainSDK/TowerDefense/TowerDefenseFlowField.cs'
       Needle = 'SignedAngle(referenceStep'
       Why = 'path spreading drifts to one corner again -- the horde becomes a single line' },
    @{ File = 'Domain/TowerDefense/TowerDefenseTerrainView.cs'
       Needle = 'MakeFloorDecal(laneRenderer'
       Why = 'the path overlay writes depth again and slices unit sprites in half' },
    @{ File = 'Domain/TowerDefense/TowerDefensePlacement.cs'
       Needle = 'animator.enabled = false'
       Why = 'the build preview ghost animates again and reads as an already-built unit' }
)

# ★ 앵커 경로는 $Root 에 기대면 안 된다 — 커밋 범위 검사 모드에서는 $Root 가 비어 있어서
#   전부 「파일 없음」으로 잡히고 **멀쩡한 푸시가 막힌다**(넣자마자 실제로 그랬다).
#   스크립트 위치에서 곧바로 계산한다.
$anchorRoot = Join-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) 'Assets/_WitchMendokusai'

# ---------------------------------------------------------------------------
# FORBIDDEN -- "this must NOT come back" checks.
#
# The mirror of ANCHOR. When a feature is removed, the text that teaches it tends to survive --
# the screen keeps explaining something that no longer exists, and the player follows it and
# nothing happens. Hit four times on 2026-08-06 alone. If a screen lies once, the whole screen
# stops being trustworthy.
#
# Remove an entry here the day the feature legitimately returns.
#
# NOTE: this file must stay UTF-8 **with BOM**. Windows PowerShell 5.1 reads a BOM-less script as
# the ANSI codepage, which mangles every Korean literal -- a Korean needle then silently never
# matches and the check quietly passes forever. Verified: the ASCII needle fired, the Korean one
# did not, on the exact same file that grep says contains the text.
# ---------------------------------------------------------------------------
$forbidden = @(
    @{ File = 'Domain/TowerDefense/TowerDefenseHudView.cs'
       Needle = '우클릭 판매'
       Why = 'selling is gone -- right click cancels now; the hint would teach a dead action' },
    @{ File = 'Domain/TowerDefense/TowerDefensePlacement.cs'
       Needle = 'match.TrySell('
       Why = 'right-click selling was removed on purpose (an irreversible action sitting on the undo gesture)' }
)

# ---------------------------------------------------------------------------
# RATCHET -- 이미 진 빚은 통과, 새로 지는 빚만 막는다 (2026-08-13).
#
# 코딩 지침의 두 규약(Enum 명시적 값 · 에셋 ID 네이밍)은 지금까지 사람 눈에만 있었다.
# 그런데 그냥 규칙으로 켜면 첫 판부터 빨강이다 -- 실측으로 enum 138개 중 66개, .asset
# 499개 중 107개가 이미 어긋나 있었다. 첫 판부터 빨강인 게이트는 지켜지는 게 아니라
# **꺼진다**. 그래서 집안의 다른 빚과 같은 모양(wm-dead-code-ratchet)으로 간다:
# 지금 있는 것은 기준선 파일에 적어 통과시키고, **기준선에 없는 새 위반만** 세운다.
#
# 빚을 갚으면 기준선에서 줄을 지운다. 지운 줄이 다시 나타나면 그때부터 빨강이다.
# ---------------------------------------------------------------------------

function Read-Baseline
{
    param([string]$Path)
    $set = New-Object 'System.Collections.Generic.HashSet[string]'
    # ★ 반드시 `,` 로 감싸 돌려준다. PowerShell 은 함수가 내보내는 컬렉션을 **풀어헤친다** —
    #   빈 집합을 그냥 return 하면 받는 쪽은 $null 이 되고, 그 뒤 .Contains() 가 터진다.
    #   (기준선이 비어 있을 때만 터지므로 「빚을 다 갚은 날」에만 고장 나는 함정이었다.)
    if (-not (Test-Path -LiteralPath $Path)) { return ,$set }
    foreach ($line in [System.IO.File]::ReadAllLines($Path))
    {
        $t = $line.Trim()
        if ($t -eq '' -or $t.StartsWith('#')) { continue }
        [void]$set.Add($t)
    }
    return ,$set
}

$scriptDir = $PSScriptRoot
$enumBaselinePath  = Join-Path $scriptDir 'wm-enum-value-baseline.tsv'
$assetBaselinePath = Join-Path $scriptDir 'wm-asset-name-baseline.tsv'
$enumBaseline  = Read-Baseline $enumBaselinePath
$assetBaseline = Read-Baseline $assetBaselinePath
$tuneBaselinePath  = Join-Path $scriptDir 'wm-tuning-const-baseline.tsv'
$tuneBaseline  = Read-Baseline $tuneBaselinePath

$ratchetMisses = New-Object System.Collections.ArrayList

# --- ENUM-VALUE ---------------------------------------------------------------
# Unity 는 enum 을 정수로 직렬화한다. 값을 안 적어 두면 항목을 중간에 하나 끼우는 순간
# 이미 저장된 에셋들이 **다른 항목을 가리킨다** -- 컴파일도 되고 시험도 통과하는데
# 퀘스트가 엉뚱한 사건에 반응한다. 실제로 GameEventType 에서 났던 사고다.
$enumOffenders = New-Object System.Collections.ArrayList
foreach ($file in $subjects)
{
    $text = ($file.Lines -join "`n")
    $text = [regex]::Replace($text, '/\*.*?\*/', '', 'Singleline')
    $text = [regex]::Replace($text, '//[^\n]*', '')
    foreach ($m in [regex]::Matches($text, '\benum\s+(\w+)[^{;]*\{(.*?)\}', 'Singleline'))
    {
        $enumName = $m.Groups[1].Value
        $body = $m.Groups[2].Value
        $members = @()
        foreach ($piece in $body.Split(','))
        {
            $t = $piece.Trim()
            if ($t -ne '') { $members += $t }
        }
        if ($members.Count -eq 0) { continue }
        $missing = @()
        foreach ($member in $members)
        {
            if ($member -notmatch '=') { $missing += ($member -split '\s+')[0] }
        }
        if ($missing.Count -eq 0) { continue }
        [void]$enumOffenders.Add(("{0}`t{1}" -f $file.Relative, $enumName))
    }
}
foreach ($key in $enumOffenders)
{
    if ($enumBaseline.Contains($key)) { continue }
    $parts = $key -split "`t"
    [void]$ratchetMisses.Add(("ENUM-VALUE  {0} -- enum '{1}' 의 항목에 명시적 값이 없다; fix: 각 항목에 = <정수> 를 적어라 (끝에 추가가 원칙)" -f $parts[0], $parts[1]))
}

# --- TUNING-CONST -------------------------------------------------------------
# 조작감·연출을 정하는 수치가 `private const` 로 박히면 **인스펙터에서 못 만진다.**
# 고치려면 코드를 열고 다시 빌드해야 하고, 그 순간 그 수치는 더 이상 조율되지 않는다.
# WM 에서 이건 취향이 아니라 MDD 정합이다 — 욘(개발자)이 자기 게임을 놀이처럼 손볼 수 있어야 한다.
# 정본: memo/rules/unity.md § 수치 노출 · memo/wm/dev/수치-노출.md
#
# 이름으로 고른다: 전부 막으면 epsilon·버퍼 크기 같은 정당한 상수까지 걸려 게이트가 꺼진다.
# 실측(2026-08-14): `private const` 숫자 324개 중 **조작감 이름 75개**만 이 규칙에 걸린다.
$tuneNamePattern = '(SPEED|FORCE|DURATION|HEIGHT|RADIUS|DELAY|COOLDOWN|THRESHOLD|ANGLE|DISTANCE|TIME|RATE|CHANCE|AMOUNT|OFFSET|DAMAGE|WEIGHT|SCALE|INTERVAL|MULTIPLIER)'
foreach ($file in $subjects)
{
    $lines2 = $file.Lines
    for ($k = 0; $k -lt $lines2.Length; $k++)
    {
        $code2 = Get-CodeText -Line $lines2[$k] -KeepStrings $false
        if ([string]::IsNullOrWhiteSpace($code2)) { continue }
        if ($code2 -notmatch 'private\s+const\s+(float|int|double)\s+([A-Za-z_][A-Za-z0-9_]*)\s*=') { continue }
        $name = $Matches[2]
        if ($name -notmatch $tuneNamePattern) { continue }
        $key2 = "{0}`t{1}" -f $file.Relative, $name
        if ($tuneBaseline.Contains($key2)) { continue }
        [void]$ratchetMisses.Add(("TUNING-CONST  {0} -- `{1}` 이 private const 다; fix: [SerializeField] 또는 SO 로 빼서 인스펙터에서 만질 수 있게" -f $file.Relative, $name))
    }
}

# --- ASSET-NAME ---------------------------------------------------------------
# 규격 = {타입}_{ID}_{이름}.asset (예: Q_5000_나무의성질연구.asset). ID 범위를 타입별로
# 나눠 충돌을 막는 장치라, 이름이 어긋나면 그 장치가 없는 것과 같다.
$assetSubjects = New-Object System.Collections.ArrayList
if ($commitScoped)
{
    foreach ($path in $Paths)
    {
        if ([string]::IsNullOrWhiteSpace($path)) { continue }
        if (-not $path.EndsWith('.asset')) { continue }
        # 이 커밋에서 지워진 파일은 판단 대상이 아니다.
        & git cat-file -e "${Sha}:${path}" 2>$null
        if ($LASTEXITCODE -ne 0) { continue }
        [void]$assetSubjects.Add(($path -replace '^Assets/_WitchMendokusai/', ''))
    }
}
else
{
    $rootFullForAssets = (Resolve-Path $Root).Path
    foreach ($file in (Get-ChildItem -Path $Root -Filter *.asset -Recurse -File))
    {
        [void]$assetSubjects.Add($file.FullName.Substring($rootFullForAssets.Length).TrimStart('\', '/').Replace('\', '/'))
    }
}
foreach ($relative in $assetSubjects)
{
    $name = Split-Path $relative -Leaf
    if ($name -match '^[A-Za-z]+_\d+_.+\.asset$') { continue }
    if ($assetBaseline.Contains($relative)) { continue }
    [void]$ratchetMisses.Add(("ASSET-NAME  {0} -- 이름이 규격에 안 맞는다; fix: {{타입}}_{{ID}}_{{이름}}.asset (예: Q_5000_나무의성질연구.asset)" -f $relative))
}

$anchorMisses = New-Object System.Collections.ArrayList
foreach ($anchor in $anchors)
{
    $full = Join-Path $anchorRoot $anchor.File
    if (-not (Test-Path -LiteralPath $full))
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

foreach ($ban in $forbidden)
{
    $full = Join-Path $anchorRoot $ban.File
    if (-not (Test-Path -LiteralPath $full)) { continue }
    $text = Get-Content -Raw -LiteralPath $full
    if ($text -like ("*" + $ban.Needle + "*"))
    {
        [void]$anchorMisses.Add(("{0} -- came back '{1}': {2}" -f $ban.File, $ban.Needle, $ban.Why))
    }
}

if ($WriteBaseline)
{
    $enumHeader = @(
        '# wm-rule-gate ENUM-VALUE 기준선 -- 이미 진 빚. 여기 없는 새 위반만 막는다.',
        '# 갚으면 줄을 지운다. 지운 줄이 다시 나타나면 그때부터 빨강이다.',
        '# 갱신: powershell -File .github/scripts/wm-rule-gate.ps1 -WriteBaseline'
    )
    $assetHeader = @(
        '# wm-rule-gate ASSET-NAME 기준선 -- 이미 진 빚. 여기 없는 새 위반만 막는다.',
        '# 갚으면 줄을 지운다. 지운 줄이 다시 나타나면 그때부터 빨강이다.',
        '# 갱신: powershell -File .github/scripts/wm-rule-gate.ps1 -WriteBaseline'
    )
    $tuneHeader = @(
        '# wm-rule-gate TUNING-CONST 기준선 -- 이미 진 빚. 여기 없는 새 위반만 막는다.',
        '# 갱신: powershell -File .github/scripts/wm-rule-gate.ps1 -WriteBaseline'
    )
    $tuneLines = @()
    foreach ($file in $subjects)
    {
        $ls = $file.Lines
        for ($k = 0; $k -lt $ls.Length; $k++)
        {
            $c = Get-CodeText -Line $ls[$k] -KeepStrings $false
            if ([string]::IsNullOrWhiteSpace($c)) { continue }
            if ($c -notmatch 'private\s+const\s+(float|int|double)\s+([A-Za-z_][A-Za-z0-9_]*)\s*=') { continue }
            # ★ 이름을 **먼저 담는다**. 다음 -notmatch 가 $Matches 를 덮어써서, 안 담으면
            #   기준선에 이름이 빈 줄이 들어간다(실측: 49줄 전부 이름 없이 저장됐다).
            $tuneName = $Matches[2]
            if ($tuneName -notmatch $tuneNamePattern) { continue }
            $tuneLines += ("{0}`t{1}" -f $file.Relative, $tuneName)
        }
    }
    $tuneLines = @($tuneLines | Sort-Object -Unique)
    Write-BaselineFile -Path $tuneBaselinePath -Lines ($tuneHeader + $tuneLines)

    $enumLines = @($enumOffenders | Sort-Object -Unique)
    $assetLines = @()
    foreach ($relative in $assetSubjects)
    {
        $name = Split-Path $relative -Leaf
        if ($name -match '^[A-Za-z]+_\d+_.+\.asset$') { continue }
        $assetLines += $relative
    }
    $assetLines = @($assetLines | Sort-Object -Unique)
    Write-BaselineFile -Path $enumBaselinePath -Lines ($enumHeader + $enumLines)
    Write-BaselineFile -Path $assetBaselinePath -Lines ($assetHeader + $assetLines)
    Write-Host ("wm-rule-gate -- 기준선을 새로 썼다: enum {0}건 / asset {1}건 / tuning {2}건" -f $enumLines.Count, $assetLines.Count, $tuneLines.Count)
    exit 0
}

$total = 0
foreach ($rule in $rules) { $total += $findings[$rule.Id].Count }
$total += $anchorMisses.Count
$total += $ratchetMisses.Count

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

# ── [META] 짝 잃은 .cs (2026-08-16) ───────────────────────────────────────────
# 왜: 유니티는 파일마다 .meta 를 만들고 그 안의 guid 로 참조를 잇는다. .cs 만 올라가면
#     받는 쪽 유니티가 <b>새 guid 를 다시 만들어</b> 프리팹·씬의 연결이 끊긴다.
#     에디터가 안 켜진 채로 새 파일을 만들면 .meta 가 아직 없어서 그대로 빠지기 쉽다(실측).
#     ★ 폴더도 마찬가지다 — 폴더 .meta 가 빠지면 받는 쪽에서 폴더 guid 가 새로 생겨
#       그 안의 것을 참조하던 자리가 통째로 흔들린다(2026-08-16 실측: Versus 폴더 4개가 빠져 있었다).
# 이미 있던 빚 — 남의 에셋 폴더 하나가 예전부터 .meta 없이 있다. 새 위반만 막는다(RATCHET 과 같은 태도).
$metaBaseline = @('Lab\Animation\Universal Animation Library[Standard]')

# .meta 는 <b>Assets 아래에만</b> 필요하다. 검사 뿌리가 저장소 루트로 들어오면 UserSettings·Tools 까지 훑어
# 수천 건이 거짓으로 잡힌다(2026-08-16 실측 6079건 — pre-push 가 이것 때문에 막혔다).
# 그리고 push 검사(커밋 범위 모드)에서는 <b>이번에 올리는 파일만</b> 본다 — 남의 미완성 편집으로 내 push 가 막히면 안 된다.
$metaBaseline = @('Lab\Animation\Universal Animation Library[Standard]')
$metaMisses = @()

$repoRootForMeta = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$metaScoped = ($PathsFrom -or ($Paths -and $Paths.Count -gt 0))

if ($metaScoped)
{
    $scopedPaths = if ($PathsFrom) { Get-Content $PathsFrom } else { $Paths }

    foreach ($relative in $scopedPaths)
    {
        if ($relative -notlike 'Assets/*' -and $relative -notlike 'Assets\*') { continue }
        if ($relative -notlike '*.cs') { continue }

        $full = Join-Path $repoRootForMeta $relative
        if (-not (Test-Path -LiteralPath $full)) { continue }
        if (Test-Path -LiteralPath ($full + '.meta')) { continue }

        $metaMisses += $relative
    }
}
else
{
    $metaRoot = if ($Root -and $Root -like '*Assets*') { $Root } else { Join-Path $repoRootForMeta 'Assets' }

    if (Test-Path -LiteralPath $metaRoot)
    {
        foreach ($source in Get-ChildItem -Path $metaRoot -Filter *.cs -Recurse)
        {
            if ($source.FullName -like '*\obj\*' -or $source.FullName -like '*\bin\*') { continue }
            if (Test-Path -LiteralPath ($source.FullName + '.meta')) { continue }
            $metaMisses += $source.FullName.Substring($metaRoot.Length).Trim('\')
        }

        foreach ($folder in Get-ChildItem -Path $metaRoot -Directory -Recurse)
        {
            if ($folder.FullName -like '*\obj\*' -or $folder.FullName -like '*\bin\*') { continue }
            # ★ -LiteralPath 필수: 폴더 이름에 대괄호가 있으면(Universal Animation Library[Standard])
            #   Test-Path 가 그걸 **와일드카드**로 읽어 「.meta 가 없다」는 거짓 빨강을 낸다 (2026-08-17 실측).
            if (Test-Path -LiteralPath ($folder.FullName + '.meta')) { continue }

            $relativeFolder = $folder.FullName.Substring($metaRoot.Length).Trim('\')
            if ($metaBaseline -contains $relativeFolder) { continue }

            $metaMisses += $relativeFolder + '  (폴더)'
        }
    }
}

if ($metaMisses.Count -gt 0)
{
    Write-Host ("  FAIL  [META] .meta 가 없는 .cs -- {0}건; fix: 에디터를 한 번 켜거나 .meta 를 같이 만들어 커밋" -f $metaMisses.Count)
    foreach ($miss in $metaMisses) { Write-Host ("          " + $miss) }
    $total = $total + $metaMisses.Count
}

Write-Host ''
if ($total -eq 0)
{
    Write-Host '  PASS  [META] 모든 .cs 에 .meta 가 있다'
    Write-Host '  PASS  [ANCHOR] required wiring lines are still present'
    Write-Host ('  PASS  [RATCHET] 새 위반 없음 (기준선: enum {0}건 / asset {1}건 / tuning {2}건 -- 이미 진 빚)' -f $enumBaseline.Count, $assetBaseline.Count, $tuneBaseline.Count)
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

if ($ratchetMisses.Count -eq 0)
{
    Write-Host ('  PASS  [RATCHET] 새 위반 없음 (기준선: enum {0}건 / asset {1}건 / tuning {2}건 -- 이미 진 빚)' -f $enumBaseline.Count, $assetBaseline.Count, $tuneBaseline.Count)
}
else
{
    Write-Host ("  FAIL  [RATCHET] 기준선에 없는 새 위반 -- {0}건 (이미 있던 빚은 통과시킨다)" -f $ratchetMisses.Count)
    $shownRatchet = 0
    foreach ($miss in $ratchetMisses)
    {
        Write-Host ("          " + $miss)
        $shownRatchet++
        if ($shownRatchet -ge $MaxShown) { break }
    }
}

Write-Host ("RESULT: FAIL -- {0} rule violation(s). Rule text: WitchMendokusai/CLAUDE.md" -f $total)
exit 1
