# wm-idle-smoke.ps1 — 구운 방치형이 <실제로 도는지> 본다 (TASK-WM-406).
#
# ★ 왜 필요한가 (실측 2026-08-16):
#   방치형 빌드는 `ManagedStrippingLevel = High` 로 굽는다(233.9 → 186.6 MB).
#   덜어내기는 <b>리플렉션으로만 쓰이는 코드를 런타임에 없애</b> 조용히 깨뜨린다 —
#   빌드는 초록으로 끝나고 켜야 알 수 있다. 즉 <b>빌드 성공은 증거가 아니다.</b>
#   한 번은 손으로 확인했지만 손으로 한 확인은 썩는다. 그래서 기계가 매번 본다.
#
# ★ 무엇을 보나 — 「안 죽었다」로는 부족하다. 창만 뜨고 판이 안 도는 것도 실패다.
#   그래서 <b>저장 파일</b>을 본다: 처치 수가 늘어야 판이 실제로 흐른 것이다.
#
# exit:
#   0 = 돈다
#   1 = 안 돈다 (예외 / 판이 안 흐름)
#   2 = 검사를 못 돌렸음 (빌드 없음 등) — 「에러 0건」과 구분한다
#
# Usage:
#   powershell -File .github/scripts/wm-idle-smoke.ps1
#   powershell -File .github/scripts/wm-idle-smoke.ps1 -BuildDir C:\wm-builds\idle -Seconds 25

param(
    [string]$BuildDir = 'C:\wm-builds\idle',
    [int]$Seconds = 25,
    [string]$SavePath = "$env:USERPROFILE\AppData\LocalLow\KarmoDDrine\WitchMendokusai\idle.json"
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

function Fail2($message)
{
    Write-Host "[idle-smoke] CANNOT-RUN: $message" -ForegroundColor Red
    Write-Host "[idle-smoke]   안 돈다는 뜻이 아니라 <검사를 못 돌렸다>는 뜻이다." -ForegroundColor DarkGray
    exit 2
}

function Fail($message)
{
    Write-Host "[idle-smoke] ❌ $message" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $BuildDir)) { Fail2 "빌드 폴더가 없다: $BuildDir" }

# ★ <플레이어 exe 가 든> 가장 새 폴더를 고른다. 그냥 「가장 새 폴더」로 고르면
#   크래시로 텅 빈 폴더를 집어 「아무것도 안 나왔다」를 초록으로 읽는다(본편 스모크가 겪은 함정).
$exe = $null
foreach ($dir in (Get-ChildItem $BuildDir -Directory | Sort-Object LastWriteTime -Descending))
{
    $candidate = Join-Path $dir.FullName 'Idle.exe'
    if (Test-Path $candidate) { $exe = $candidate; break }
    Write-Host "[idle-smoke] WARN: 플레이어가 없는 빌드 폴더를 건너뛴다 — $($dir.Name)" -ForegroundColor Yellow
}

if ($null -eq $exe) { Fail2 "플레이어가 든 빌드가 하나도 없다: $BuildDir" }

Write-Host "[idle-smoke] exe    : $exe"
Write-Host "[idle-smoke] 굴린다 : $Seconds 초"

# 판이 <새로> 흘렀는지 보려면 지난 저장이 없어야 한다.
if (Test-Path $SavePath) { Remove-Item $SavePath -Force }

$log = Join-Path ([System.IO.Path]::GetTempPath()) 'wm-idle-smoke.log'
if (Test-Path $log) { Remove-Item $log -Force }

$process = Start-Process -FilePath $exe -PassThru `
    -ArgumentList @('-logFile', $log, '-screen-width', '800', '-screen-height', '600', '-screen-fullscreen', '0')

Start-Sleep -Seconds $Seconds

if (-not $process.HasExited)
{
    $process.Kill()
    $process.WaitForExit(10000) | Out-Null
}
else
{
    Fail "켜자마자 스스로 꺼졌다 (exit $($process.ExitCode)) — 로그: $log"
}

Start-Sleep -Seconds 1

if (-not (Test-Path $log)) { Fail2 "로그가 안 생겼다 — 실행 자체가 안 됐다: $log" }

$logText = Get-Content $log -Raw
$bad = Select-String -Path $log -Pattern 'Exception|NullReference|MissingMethod|TypeLoadException' -ErrorAction SilentlyContinue

if ($bad)
{
    Write-Host "[idle-smoke] 로그에 예외가 있다:" -ForegroundColor Red
    $bad | Select-Object -First 5 | ForEach-Object { Write-Host "  $($_.Line.Trim())" -ForegroundColor DarkGray }
    Fail "덜어내기가 무언가를 깨뜨렸을 수 있다 (ManagedStrippingLevel=High)"
}

# ★ 여기가 핵심 — 창만 뜨고 판이 안 도는 것도 실패다.
if (-not (Test-Path $SavePath)) { Fail "저장 파일이 안 생겼다 — 화면은 떴어도 판이 안 돌았다: $SavePath" }

$save = Get-Content $SavePath -Raw | ConvertFrom-Json

if ($save.Kills -le 0)
{
    Fail "$Seconds 초를 굴렸는데 처치 수가 0 이다 — 시간이 안 흐른다"
}

Write-Host "[idle-smoke] ✅ 돈다 — 처치 $($save.Kills) · 자원 $($save.Resource) · $($save.Stage)단계 (예외 0건)" -ForegroundColor Green
exit 0
