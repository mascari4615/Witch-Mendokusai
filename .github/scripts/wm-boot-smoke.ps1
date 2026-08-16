<#
    본편이 <실제로 뜨는가> — 사람 없이 판정한다 (TASK-WM-409 / 원 설계 TASK-WM-118 I5b).

    ★ 왜 지금 만드나: 인프라는 <b>이미 다 있었다</b> —
        `BootSmokeBuilder`(빌드) · `BootSmokeSentinel`(판정·결과파일) · `BootMode`(WM_BOOT_DETERMINISTIC).
      없던 건 <b>실행 래퍼 하나</b>뿐이라 아무도 안 돌렸다. `CLAUDE.md` 는 이 파일이 있다고 적어 뒀지만
      실제로는 없었다(실측 2026-08-17). 그래서 본편 조립을 건드리는 작업이 전부
      「사람이 켜 봐야 안다」에 막혀 있었다.

    2단계:
      ① 빌드 — Unity -batchmode -executeMethod BootSmokeBuilder.BuildFromCLI (에디터 필요, 느림)
      ② 실행 — 구운 exe 를 -batchmode 로 켠다 (에디터 0 의존).
         센티넬이 WorldReady 도달 + NRE 0 이면 결과파일에 PASS 를 쓰고 스스로 꺼진다.

    쓰는 법:
      powershell -File .github/scripts/wm-boot-smoke.ps1              # 빌드 + 실행
      powershell -File .github/scripts/wm-boot-smoke.ps1 -SkipBuild   # 이미 구운 것으로 실행만
      powershell -File .github/scripts/wm-boot-smoke.ps1 -TimeoutSec 180

    실측 2026-08-17 첫 통과: WorldReady 4.6초 · 11,274프레임 · NRE 0 · DDOL 싱글톤 31.
    ⚠ 빌드는 1.8GB·수 분 걸린다. 이미 구웠으면 `-SkipBuild` 로 실행만 하면 20초면 끝난다.

    나가는 값: 0 = 뜬다 / 1 = 안 뜬다(결과파일 FAIL 또는 무응답) / 2 = 환경 문제
#>
param(
    [string]$Unity = 'C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Unity.exe',
    [string]$ExePath = '',
    [int]$TimeoutSec = 150,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if ([string]::IsNullOrWhiteSpace($ExePath)) { $ExePath = Join-Path $root 'Build\Smoke\WM.exe' }

$buildLog = Join-Path ([System.IO.Path]::GetTempPath()) 'wm-boot-smoke-build.log'
$runLog = Join-Path ([System.IO.Path]::GetTempPath()) 'wm-boot-smoke-run.log'
$resultFile = Join-Path ([System.IO.Path]::GetTempPath()) 'wm_boot_smoke_result.txt'

function Fail2([string]$m) { Write-Host "[boot-smoke] $m" -ForegroundColor Red; exit 2 }
function Fail([string]$m) { Write-Host "[boot-smoke] $m" -ForegroundColor Red; exit 1 }

if (-not (Test-Path $Unity)) { Fail2 "유니티가 없다: $Unity" }

if (-not $SkipBuild)
{
    # ★ 죽은 유니티가 남긴 락은 배치모드를 <즉시 exit 1> 로 죽인다 (실측 2026-08-16).
    $lock = Join-Path $root 'Temp\UnityLockfile'
    if (Test-Path $lock) { Remove-Item $lock -Force -ErrorAction SilentlyContinue }

    Write-Host "[boot-smoke] 굽는다 → $ExePath  (로그: $buildLog)"
    $args = @(
        '-batchmode', '-quit', '-projectPath', $root,
        '-executeMethod', 'WitchMendokusai.EditorTools.BootSmokeBuilder.BuildFromCLI',
        '-wmSmokeOut', $ExePath,
        '-logFile', $buildLog
    )
    $build = Start-Process -FilePath $Unity -ArgumentList $args -PassThru -Wait
    if ($build.ExitCode -ne 0) { Fail "빌드 실패 (exit $($build.ExitCode)) — 로그: $buildLog" }
}

if (-not (Test-Path $ExePath)) { Fail2 "구운 exe 가 없다: $ExePath  (-SkipBuild 를 뺐는지 확인)" }

if (Test-Path $resultFile) { Remove-Item $resultFile -Force }
if (Test-Path $runLog) { Remove-Item $runLog -Force }

# 센티넬이 <스스로> 결정적 부팅으로 돌고, 판정을 이 파일에 적는다.
$env:WM_BOOT_DETERMINISTIC = '1'
$env:WM_BOOT_SMOKE_RESULT = $resultFile

Write-Host "[boot-smoke] 켠다 (최대 $TimeoutSec 초) — $ExePath"
$run = Start-Process -FilePath $ExePath -PassThru `
    -ArgumentList @('-batchmode', '-logFile', $runLog, '-screen-width', '800', '-screen-height', '600', '-screen-fullscreen', '0')

if (-not $run.WaitForExit($TimeoutSec * 1000))
{
    $run.Kill()
    $run.WaitForExit(10000) | Out-Null
    Fail "$TimeoutSec 초 안에 안 끝났다 — 부팅이 어딘가에서 멈췄다. 로그: $runLog"
}

Start-Sleep -Milliseconds 500

if (-not (Test-Path $resultFile))
{
    Write-Host "[boot-smoke] 결과파일이 없다 — 센티넬이 판정을 못 냈다 (exit $($run.ExitCode))" -ForegroundColor Red
    if (Test-Path $runLog)
    {
        Select-String -Path $runLog -Pattern 'Exception|NullReference|\[BOOT' -ErrorAction SilentlyContinue |
            Select-Object -First 8 | ForEach-Object { Write-Host "  $($_.Line.Trim())" -ForegroundColor DarkGray }
    }
    Fail "판정 없음 — 로그: $runLog"
}

$body = (Get-Content $resultFile -Raw).Trim()
Write-Host "[boot-smoke] 결과파일:" -ForegroundColor DarkGray
$body -split "`n" | Select-Object -First 8 | ForEach-Object { Write-Host "  $_" -ForegroundColor DarkGray }

if ($body -match 'FAIL')
{
    Fail "본편이 안 뜬다 — 위 사유 참조 · 로그: $runLog"
}
if ($body -notmatch 'PASS')
{
    Fail "결과를 못 읽었다 (PASS/FAIL 둘 다 없음) — $resultFile"
}

Write-Host "[boot-smoke] ✅ 본편이 뜬다 — WorldReady 도달 · NRE 0 (exit $($run.ExitCode))" -ForegroundColor Green
exit 0
