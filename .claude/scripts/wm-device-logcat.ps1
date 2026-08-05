<#
.SYNOPSIS
  Pulls Android device logs (`adb logcat`) to disk — the fallback layer the
  in-app relay (TASK-WM-201) structurally cannot cover.

.DESCRIPTION
  The canonical channel for phone runtime logs is the in-app relay:
  `DeviceLogRelay` posts to yawnbot, and the logs are readable at
  `https://yawnbot.mascari4615.com/device-log` (human) and
  `GET /device-log/tail` (AI). That path needs no cable and survives reboots.

  It cannot, however, capture what happens *below* the C# layer:

    - native crashes (IL2CPP / Java) that kill the process before a flush
    - Unity player startup failures before `RuntimeInitializeOnLoadMethod`
    - ANRs, OOM kills, and the Android tombstone

  Those only exist in logcat. This script is that layer: connect the phone by
  USB (or `adb connect <ip>` over Wi-Fi), run it, get a timestamped file that
  Claude can read directly.

  `adb` resolution order: `-AdbPath` → `adb` on PATH → Unity's bundled
  platform-tools (Unity Hub Android module). If none exist the script says so
  and points at the install, rather than failing with a bare "not found".

.PARAMETER OutDir
  Where to write the capture. Default `<repo>/Build/logcat`.

.PARAMETER Filter
  Regex applied to captured lines. Default keeps Unity + crash-relevant tags.
  Pass `.` to keep everything.

.PARAMETER Seconds
  Live-capture duration. 0 (default) = dump the existing buffer and exit,
  which is what you want right after a crash.

.PARAMETER Clear
  Clear the device log buffer *before* capturing. Use with -Seconds to get a
  clean reproduction window.

.PARAMETER AdbPath
  Explicit path to adb.exe.

.EXAMPLE
  # 방금 튕겼다 — 지금 버퍼 통째로 회수
  pwsh .claude/scripts/wm-device-logcat.ps1

.EXAMPLE
  # 깨끗한 창에서 60초 재현 캡처
  pwsh .claude/scripts/wm-device-logcat.ps1 -Clear -Seconds 60
#>
[CmdletBinding()]
param(
  [string]$OutDir,
  [string]$Filter = 'Unity|WM|CRASH|DEBUG|AndroidRuntime|libunity|il2cpp|tombstone|FATAL|ANR',
  [int]$Seconds = 0,
  [switch]$Clear,
  [string]$AdbPath
)

$ErrorActionPreference = 'Stop'

function Resolve-Adb {
  param([string]$Explicit)

  if ($Explicit) {
    if (Test-Path $Explicit) { return $Explicit }
    throw "지정한 adb 를 못 찾았다: $Explicit"
  }

  $onPath = Get-Command adb -ErrorAction SilentlyContinue
  if ($onPath) { return $onPath.Source }

  # Unity Hub 의 안드로이드 모듈이 platform-tools 를 함께 깐다.
  $hubRoots = @(
    "$env:ProgramFiles\Unity\Hub\Editor",
    "$env:LOCALAPPDATA\Unity\Hub\Editor"
  ) | Where-Object { Test-Path $_ }

  foreach ($root in $hubRoots) {
    $candidate = Get-ChildItem -Path $root -Directory -ErrorAction SilentlyContinue |
      ForEach-Object {
        Join-Path $_.FullName 'Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe'
      } | Where-Object { Test-Path $_ } | Select-Object -First 1
    if ($candidate) { return $candidate }
  }

  throw @'
adb 를 못 찾았다. 셋 중 하나로 해결한다:
  1. Unity Hub → 설치된 에디터 → 모듈 추가 → Android Build Support (SDK 포함)  ← 빌드머신이면 이게 정답
  2. Android platform-tools 를 따로 설치하고 PATH 에 추가
  3. -AdbPath 로 adb.exe 경로 직접 지정

참고: 케이블이 필요 없는 정본 경로는 앱 내장 릴레이다(TASK-WM-201).
      https://yawnbot.mascari4615.com/device-log — 이 스크립트는 네이티브 크래시 전용 보조.
'@
}

$adb = Resolve-Adb -Explicit $AdbPath
Write-Host "[logcat] adb = $adb"

$devices = & $adb devices | Select-Object -Skip 1 | Where-Object { $_ -match '\S' -and $_ -notmatch 'offline' }
if (-not $devices) {
  throw @'
연결된 기기가 없다.
  · USB 케이블 연결 + 폰에서 「USB 디버깅 허용」 확인
  · 또는 무선: 폰 개발자 옵션에서 무선 디버깅 켜고 `adb connect <폰IP>:<포트>`
'@
}
Write-Host "[logcat] 기기: $($devices -join ', ')"

if (-not $OutDir) {
  $repoRoot = (& git rev-parse --show-toplevel 2>$null)
  if (-not $repoRoot) { $repoRoot = (Get-Location).Path }
  $OutDir = Join-Path $repoRoot 'Build/logcat'
}
if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Force -Path $OutDir | Out-Null }

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$outFile = Join-Path $OutDir "logcat-$stamp.txt"

if ($Clear) {
  & $adb logcat -c
  Write-Host '[logcat] 기기 버퍼 비움'
}

if ($Seconds -gt 0) {
  Write-Host "[logcat] $Seconds 초 동안 잡는다 — 지금 폰에서 재현해라"
  $process = Start-Process -FilePath $adb -ArgumentList 'logcat', '-v', 'threadtime' `
    -RedirectStandardOutput $outFile -NoNewWindow -PassThru
  Start-Sleep -Seconds $Seconds
  if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force }
} else {
  Write-Host '[logcat] 현재 버퍼를 통째로 회수'
  & $adb logcat -d -v threadtime | Out-File -FilePath $outFile -Encoding utf8
}

$all = @(Get-Content $outFile -ErrorAction SilentlyContinue)
$matched = @($all | Where-Object { $_ -match $Filter })
$filteredFile = Join-Path $OutDir "logcat-$stamp.filtered.txt"
$matched | Out-File -FilePath $filteredFile -Encoding utf8

# 조용해도 결과를 말한다 — 「파일이 없다」가 정상인지 실패인지 구분 못 하는 상태를 안 만든다.
Write-Host ''
Write-Host "[logcat] 전체 $($all.Count) 줄 → $outFile"
Write-Host "[logcat] 필터 통과 $($matched.Count) 줄 → $filteredFile"
if ($matched.Count -eq 0) {
  Write-Host '[logcat] 필터에 걸린 줄이 0 이다. 앱을 실행한 뒤 다시 잡거나, -Filter . 로 전부 받아라.'
}

$fatal = @($all | Where-Object { $_ -match 'FATAL EXCEPTION|signal \d+ \(SIG|beginning of crash' })
if ($fatal.Count -gt 0) {
  Write-Host ''
  Write-Host "[logcat] 크래시 흔적 $($fatal.Count) 줄:" -ForegroundColor Red
  $fatal | Select-Object -First 20 | ForEach-Object { Write-Host "  $_" }
}
