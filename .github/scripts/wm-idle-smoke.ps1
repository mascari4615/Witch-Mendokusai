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
#   1 = 안 돈다 (예외 / 판이 안 흐름 / 밖으로 부른다)
#   2 = 검사를 못 돌렸음 (빌드 없음 등) — 「에러 0건」과 구분한다
#
# Usage:
#   powershell -File .github/scripts/wm-idle-smoke.ps1
#   powershell -File .github/scripts/wm-idle-smoke.ps1 -BuildDir C:\wm-builds\idle -Seconds 25

param(
    # ★ 부르는 쪽이 <어느 exe 인지> 알면 그걸 준다 — 추측이 끼면 엉뚱한 판을 검사한다.
    #   (빌드 워크플로는 방금 구운 경로를 안다. 사람이 손으로 부를 때만 아래 폴더 추측을 쓴다.)
    [string]$ExePath,
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

# ★ 화면 없는 세션(세션 0)에서는 <b>이 검사가 성립하지 않는다</b>.
#   플레이어는 창을 띄워야 판이 도는데 LocalSystem 서비스에는 붙을 데스크톱이 없다.
#   그대로 두면 「저장 파일이 안 생겼다」로 <b>게임이 깨진 것처럼</b> 빨개진다 —
#   실측 2026-08-16: 노트북 러너(actions.runner…karmo-laptop-wm)가 LocalSystem 서비스라
#   저장 경로가 C:\WINDOWS\system32\config\systemprofile\... 로 잡혔다.
#   「검사를 못 돌렸다」와 「안 돈다」는 다른 말이고, 섞으면 빨간불이 뜻을 잃는다.
if ([Environment]::UserName -eq 'SYSTEM' -or $env:USERPROFILE -like '*systemprofile*')
{
    Fail2 "화면 없는 세션이다 (계정 $([Environment]::UserName)) — 플레이어를 띄울 데스크톱이 없다. 사람 세션에서 돌릴 것"
}

$exe = $null

if (-not [string]::IsNullOrWhiteSpace($ExePath))
{
    if (-not (Test-Path $ExePath)) { Fail2 "준 경로에 플레이어가 없다: $ExePath" }
    $exe = $ExePath
}
else
{
    if (-not (Test-Path $BuildDir)) { Fail2 "빌드 폴더가 없다: $BuildDir" }

    # ★ <플레이어 exe 가 든> 가장 새 폴더를 고른다. 그냥 「가장 새 폴더」로 고르면
    #   크래시로 텅 빈 폴더를 집어 「아무것도 안 나왔다」를 초록으로 읽는다(본편 스모크가 겪은 함정).
    foreach ($dir in (Get-ChildItem $BuildDir -Directory | Sort-Object LastWriteTime -Descending))
    {
        $candidate = Join-Path $dir.FullName 'Idle.exe'
        if (Test-Path $candidate) { $exe = $candidate; break }
        Write-Host "[idle-smoke] WARN: 플레이어가 없는 빌드 폴더를 건너뛴다 — $($dir.Name)" -ForegroundColor Yellow
    }

    if ($null -eq $exe) { Fail2 "플레이어가 든 빌드가 하나도 없다: $BuildDir" }
}

Write-Host "[idle-smoke] exe    : $exe"
Write-Host "[idle-smoke] 굴린다 : $Seconds 초"

# 판이 <새로> 흘렀는지 보려면 지난 저장이 없어야 한다.
#
# ⚠ 본 파일만 지우면 <b>모자란다</b> (2026-08-17): 이제 저장이 직전 판(.bak)을 남기고,
#   본 파일이 없으면 게임이 그 직전 판으로 되살아난다. 그러면 이 검사는 <b>지난 판</b>을
#   물려받은 채로 「처치 수가 늘었다」를 통과한다 — 눈뜬장님이 된다.
$saveKin = @($SavePath, "$SavePath.bak", "$SavePath.broken", "$SavePath.tmp")
foreach ($one in $saveKin) { if (Test-Path $one) { Remove-Item $one -Force } }

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

# ★ 팔 게임이 <남의 서버>를 부르면 실패다 (TASK-WM-406).
#   2026-08-16 실측: 방치형 exe 가 본편 텔레메트리를 스스로 띄워 `[DeviceLog] 전송 실패 (401)`.
#   `#if !WM_IDLE` 로 막았지만, 그 한 줄이 지워지거나 표식이 안 붙으면 조용히 되돌아온다 —
#   예외가 아니라서 위 검사는 초록으로 통과한다. 그래서 <b>여기서 따로 본다</b>.
$phone = Select-String -Path $log -Pattern '\[DeviceLog\]|\[BuildStamp\]|X-Yawnbot-Secret|yawnbot\.mascari4615\.com|UnityWebRequest|Curl error' -ErrorAction SilentlyContinue

if ($phone)
{
    Write-Host "[idle-smoke] 이 빌드가 밖으로 무언가를 부른다:" -ForegroundColor Red
    $phone | Select-Object -First 5 | ForEach-Object { Write-Host "  $($_.Line.Trim())" -ForegroundColor DarkGray }
    Fail "팔 게임에 본편 진단 장치가 실렸다 — WM_IDLE 표식과 `#if !WM_IDLE` 가드를 확인할 것"
}

# ★ 여기가 핵심 — 창만 뜨고 판이 안 도는 것도 실패다.
if (-not (Test-Path $SavePath)) { Fail "저장 파일이 안 생겼다 — 화면은 떴어도 판이 안 돌았다: $SavePath" }

$save = Get-Content $SavePath -Raw | ConvertFrom-Json

if ($save.Kills -le 0)
{
    Fail "$Seconds 초를 굴렸는데 처치 수가 0 이다 — 시간이 안 흐른다"
}

Write-Host "[idle-smoke] 돈다 — 처치 $($save.Kills) · 자원 $($save.Resource) · $($save.Stage)단계 (예외 0건)"

# ─────────────────────────────────────────────────────────────────────────────
# ★ 두 번째 판 — <b>저장을 되읽나</b> (TASK-WM-406, 2026-08-17).
#
#   여기까지는 「새 판이 돈다」만 봤다. 그런데 방치형에서 제일 비싼 고장은
#   <b>저장을 못 읽는 것</b>이다 — 몇 주치가 조용히 사라지고, 그때 게임은 멀쩡히 돈다.
#   덜어내기(High)는 직렬화 코드를 특히 잘 깨뜨리는데, 위 검사는 그걸 <b>초록으로</b> 통과한다.
#
#   그래서 눈에 띄는 표를 하나 박아 넣고 다시 켠다. 그 표가 살아 있으면 되읽은 것이다.
$mark = 123456
$save.Kills = $mark
$save | ConvertTo-Json -Depth 12 | Set-Content -Path $SavePath -Encoding UTF8

# ⚠ 그냥 「다시 켜고 잠깐 뒤에 본다」로는 <b>아무것도 안 본다</b> (2026-08-17에 하마터면):
#   게임이 그 사이에 한 번도 안 적으면 파일은 내가 쓴 그대로라 검사가 무조건 통과한다.
#   그래서 <b>게임이 다시 적을 때까지 기다린다</b> — 시간을 박지 말고 「적었나」를 본다.
$stamp = (Get-Item $SavePath).LastWriteTimeUtc

Write-Host "[idle-smoke] 다시 켠다 — 저장을 되읽는지 본다 (처치 $mark 를 박아 뒀다)"

$log2 = Join-Path ([System.IO.Path]::GetTempPath()) 'wm-idle-smoke-2.log'
if (Test-Path $log2) { Remove-Item $log2 -Force }

$second = Start-Process -FilePath $exe -PassThru `
    -ArgumentList @('-logFile', $log2, '-screen-width', '800', '-screen-height', '600', '-screen-fullscreen', '0')

$wroteAgain = $false

for ($waited = 0; $waited -lt 60; $waited++)
{
    Start-Sleep -Milliseconds 500

    if ((Get-Item $SavePath).LastWriteTimeUtc -gt $stamp)
    {
        $wroteAgain = $true
        break
    }

    if ($second.HasExited) { break }
}

if (-not $second.HasExited)
{
    $second.Kill()
    $second.WaitForExit(10000) | Out-Null
}

Start-Sleep -Seconds 1

if (-not $wroteAgain)
{
    Fail2 "다시 켠 판이 30초 안에 한 번도 안 적었다 — 되읽었는지 <잴 수가 없다> (로그: $log2)"
}

$after = Get-Content $SavePath -Raw | ConvertFrom-Json

if ($after.Kills -lt $mark)
{
    Write-Host "[idle-smoke]   박아 둔 처치 $mark · 다시 적힌 뒤 $($after.Kills)" -ForegroundColor DarkGray
    Fail "저장을 안 읽고 처음부터 시작했다 — 사람이 몇 주치를 잃는 고장이다 (로그: $log2)"
}

Write-Host "[idle-smoke] ✅ 돈다 + 저장을 되읽는다 — 처치 $($after.Kills) · $($after.Stage)단계 (예외 0건)" -ForegroundColor Green
exit 0
