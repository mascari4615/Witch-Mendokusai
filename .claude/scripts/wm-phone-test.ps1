<#
.SYNOPSIS
  One command for a real-phone check of a laptop Android build: fetch, verify, install, launch, log, screenshot.

.DESCRIPTION
  2026-11-17: every step below was done by hand and each one broke once
  (password-gated link, corrupted dev APK, Windows catalog in an Android APK,
  locked phone pausing the app, release build without remote logs).

    1. fetch   laptop-ops signed link with the ops Bearer token (~/.laptop-ops-token)
    2. verify  .github/scripts/wm-verify-apk.ps1 (zip integrity + Addressables catalog platform)
    3. install adb install -r
    4. unlock  refuses to launch while the keyguard is showing (the app would pause at once)
    5. launch  monkey launcher intent, wait, then logcat --pid and a screencap

  ASCII only (PowerShell 5.1 reads BOM-less files as cp949).

.PARAMETER Build
  Laptop build folder name, e.g. 20261117-182057-282-android. Omit to use -Apk.
.PARAMETER Apk
  Local APK path instead of fetching.
.PARAMETER WaitSec
  Seconds to let the game boot before reading the log. Default 25.
.PARAMETER OutDir
  Where the APK, log and screenshot go. Default <repo>/Build/phone-test.
#>
param(
  [string]$Build,
  [string]$Apk,
  [int]$WaitSec = 25,
  [string]$OutDir,
  [string]$Package = 'com.Karmotrine.WitchIdle'
)
$ErrorActionPreference = 'Stop'
$repo = Resolve-Path (Join-Path $PSScriptRoot '..\..')
if (-not $OutDir) { $OutDir = Join-Path $repo 'Build\phone-test' }
New-Item -ItemType Directory -Force $OutDir | Out-Null

function Resolve-Adb {
  $onPath = Get-Command adb -ErrorAction SilentlyContinue
  if ($onPath) { return $onPath.Source }
  $local = Join-Path $HOME '.karmoddrine\android\platform-tools\adb.exe'
  if (Test-Path $local) { return $local }
  throw 'adb not found. Install Google platform-tools to ~/.karmoddrine/android/platform-tools.'
}
$adb = Resolve-Adb

# 1. fetch
if (-not $Apk) {
  if (-not $Build) { throw 'pass -Build <laptop build folder> or -Apk <path>' }
  $tokenFile = Join-Path $HOME '.laptop-ops-token'
  $token = if ($env:LAPTOP_OPS_TOKEN) { $env:LAPTOP_OPS_TOKEN.Trim() } else { (Get-Content $tokenFile -Raw).Trim() }
  $base = if ($env:LAPTOP_OPS_BASE) { $env:LAPTOP_OPS_BASE } else { 'https://laptop.mascari4615.com' }
  $headers = @{ Authorization = "Bearer $token" }
  $body = @{ build = $Build; file = 'WitchMendokusai.apk' } | ConvertTo-Json
  $signed = Invoke-RestMethod -Method Post -Uri "$base/dl/sign" -Headers $headers -ContentType 'application/json' -Body $body
  $Apk = Join-Path $OutDir "$Build.apk"
  Invoke-WebRequest -Uri $signed.url -Headers $headers -OutFile $Apk -UseBasicParsing
  Write-Host "[phone-test] fetched $Apk ($((Get-Item $Apk).Length) bytes)"
}

# 2. verify
& (Join-Path $repo '.github\scripts\wm-verify-apk.ps1') -Apk $Apk -PlatformFolder Android
if ($LASTEXITCODE -ne 0) { throw 'APK verify failed, not installing' }

# 3. install
$devices = & $adb devices | Select-String -Pattern '\tdevice$'
if (-not $devices) { throw 'no phone connected (USB debugging on, cable in, allow prompt accepted?)' }
$install = & $adb install -r $Apk 2>&1 | Select-Object -Last 1
Write-Host "[phone-test] install: $install"
if ("$install" -notmatch 'Success') { throw "install failed: $install" }

# 4. unlock check
$keyguard = & $adb shell "dumpsys window | grep -E 'isKeyguardShowing'" 2>&1 | Out-String
if ($keyguard -match 'isKeyguardShowing=true') {
  throw 'phone is locked. Unlock it and run again (a locked phone pauses the app right after launch).'
}

# 5. launch, log, screenshot
& $adb shell am force-stop $Package | Out-Null
& $adb logcat -c
& $adb shell monkey -p $Package -c android.intent.category.LAUNCHER 1 *> $null
Start-Sleep -Seconds $WaitSec
$appPid = (& $adb shell pidof $Package).Trim()
if (-not $appPid) { Write-Host '[phone-test] process not running (crash?) - dumping crash buffer'; & $adb logcat -d -b crash | Select-Object -Last 60; exit 1 }
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$log = Join-Path $OutDir "logcat-$stamp.txt"
& $adb logcat -d -v time --pid=$appPid | Out-File -FilePath $log -Encoding utf8
$shot = Join-Path $OutDir "screen-$stamp.png"
cmd /c "`"$adb`" exec-out screencap -p > `"$shot`""
$errors = Select-String -Path $log -Pattern ' E/Unity|Exception' | Select-Object -First 20
Write-Host "[phone-test] pid=$appPid log=$log screenshot=$shot unityErrors=$(@($errors).Count)"
$errors | ForEach-Object { Write-Host ('  ' + $_.Line.Substring(0, [Math]::Min(220, $_.Line.Length))) }
