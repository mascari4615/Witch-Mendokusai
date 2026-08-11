# WM.Server 를 nssm 서비스로 등록 — 노트북에서 1회 실행.
# 사용:
#   pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/install-service.ps1
#
# 전제:
#   - nssm 설치 (`winget install NSSM.NSSM` 또는 `choco install nssm`)
#   - .NET 8 SDK 설치
#   - 공개 hostname/tunnel 은 별도로 연결

[CmdletBinding()]
param(
  [string]$ServiceName = 'wm-world',
  [string]$Configuration = 'Release',
  [string]$PublishDir = 'C:\wm-world\app',
  [string]$DataDir = 'C:\wm-world\data',
  [string]$ListenUrl = 'http://127.0.0.1:5199',
  [string]$HealthUrl = '',
  [string]$WorldFile = '',
  [string]$ItemsFile = '',
  [string]$BuildingsFile = '',
  [string]$CraftsFile = '',
  [string]$GatherablesFile = '',
  [string]$IngredientsFile = '',
  [string]$RecipesFile = '',
  [string]$KarmoLabApi = '',
  [string]$KarmoLabVerify = '',
  [switch]$SkipPublish,
  [switch]$AsLocalSystem
)

$ErrorActionPreference = 'Stop'

$here = Split-Path -Parent $PSCommandPath
$serverRoot = Resolve-Path (Join-Path $here '..')
$project = Join-Path $serverRoot 'WM.Server.csproj'
Write-Host "[install] server root = $serverRoot"

$dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if (-not $dotnet) { throw '.NET SDK not found in PATH' }
Write-Host "[install] dotnet = $dotnet"

$nssm = (Get-Command nssm -ErrorAction SilentlyContinue).Source
if (-not $nssm) {
  $candidates = @(
    "$env:LOCALAPPDATA\Microsoft\WinGet\Links\nssm.exe",
    'C:\Users\masca\AppData\Local\Microsoft\WinGet\Links\nssm.exe',
    'C:\ProgramData\chocolatey\bin\nssm.exe',
    'C:\Program Files\nssm\nssm.exe',
    'C:\nssm\nssm.exe'
  )
  foreach ($candidate in $candidates) {
    if ($candidate -and (Test-Path $candidate)) { $nssm = $candidate; break }
  }
}
if (-not $nssm) { throw 'nssm not found (install: winget install NSSM.NSSM or choco install nssm)' }
Write-Host "[install] nssm = $nssm"

New-Item -ItemType Directory -Force -Path $PublishDir | Out-Null
New-Item -ItemType Directory -Force -Path $DataDir | Out-Null

if (-not $WorldFile) {
  $WorldFile = Join-Path $DataDir 'world.json'
}

if (-not $HealthUrl) {
  $HealthUrl = ($ListenUrl.TrimEnd('/') + '/health')
}

if (-not $SkipPublish) {
  Write-Host "[install] dotnet publish -> $PublishDir"
  & $dotnet publish $project -c $Configuration -o $PublishDir
  if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed' }
}

$entry = Join-Path $PublishDir 'WM.Server.exe'
if (-not (Test-Path $entry)) {
  throw "entry missing: $entry"
}

$existing = & $nssm status $ServiceName 2>$null
if ($LASTEXITCODE -eq 0) {
  Write-Host "[install] existing service found - stop + remove"
  & $nssm stop $ServiceName confirm | Out-Null
  & $nssm remove $ServiceName confirm | Out-Null
}

$environmentLines = [System.Collections.Generic.List[string]]::new()
$environmentLines.Add("ASPNETCORE_URLS=$ListenUrl")
$environmentLines.Add("WM_WORLD_FILE=$WorldFile")
if ($ItemsFile) { $environmentLines.Add("WM_ITEMS_FILE=$ItemsFile") }
if ($BuildingsFile) { $environmentLines.Add("WM_BUILDINGS_FILE=$BuildingsFile") }
if ($CraftsFile) { $environmentLines.Add("WM_CRAFTS_FILE=$CraftsFile") }
if ($GatherablesFile) { $environmentLines.Add("WM_GATHERABLES_FILE=$GatherablesFile") }
if ($IngredientsFile) { $environmentLines.Add("WM_INGREDIENTS_FILE=$IngredientsFile") }
if ($RecipesFile) { $environmentLines.Add("WM_RECIPES_FILE=$RecipesFile") }
if ($KarmoLabApi) { $environmentLines.Add("WM_KARMOLAB_API=$KarmoLabApi") }
if ($KarmoLabVerify) { $environmentLines.Add("WM_KARMOLAB_VERIFY=$KarmoLabVerify") }
$environmentBlock = [string]::Join("`n", $environmentLines)

$stdoutLog = Join-Path $DataDir 'service.out.log'
$stderrLog = Join-Path $DataDir 'service.err.log'

Write-Host "[install] registering nssm service '$ServiceName'"
& $nssm install $ServiceName $entry
& $nssm set $ServiceName AppDirectory $PublishDir
& $nssm set $ServiceName AppStdout $stdoutLog
& $nssm set $ServiceName AppStderr $stderrLog
& $nssm set $ServiceName AppRotateFiles 1
& $nssm set $ServiceName AppRotateBytes 10485760
& $nssm set $ServiceName Start SERVICE_AUTO_START
& $nssm set $ServiceName Description 'WitchMendokusai world host (TASK-WM-219)'
& $nssm set $ServiceName AppEnvironmentExtra $environmentBlock

if ($AsLocalSystem) {
  Write-Host "[install] service account = LocalSystem (--AsLocalSystem opt-in)"
} else {
  $defaultUser = "$env:USERDOMAIN\$env:USERNAME"
  Write-Host "[install] service account = user (default). current user = $defaultUser"
  $cred = Get-Credential -Message "WM.Server service account" -UserName $defaultUser
  if (-not $cred) { throw 'credential cancelled - re-run with -AsLocalSystem to fallback' }

  $plainPwd = $cred.GetNetworkCredential().Password
  if (-not $plainPwd) { throw 'password empty - re-run with -AsLocalSystem to fallback' }

  & $nssm set $ServiceName ObjectName $cred.UserName $plainPwd
  if ($LASTEXITCODE -ne 0) {
    throw 'nssm set ObjectName failed (credential rejected?)'
  }
  Write-Host "[install] service ObjectName = $($cred.UserName)"
}

Write-Host "[install] starting service"
& $nssm start $ServiceName | Out-Host
if ($LASTEXITCODE -ne 0) { throw "nssm start failed: $LASTEXITCODE" }

$healthy = $false
for ($attempt = 0; $attempt -lt 12; $attempt++) {
  Start-Sleep -Seconds 2
  try {
    $health = Invoke-RestMethod -Uri $HealthUrl -TimeoutSec 5
    if ($health.ok -eq $true) {
      $healthy = $true
      $health | ConvertTo-Json -Depth 6 | Write-Host
      break
    }
  }
  catch {
    Write-Host "[install] waiting for health ($($attempt + 1)/12): $($_.Exception.Message)"
  }
}

& $nssm status $ServiceName | Out-Host
if (-not $healthy) {
  throw "WM.Server did not become healthy at $HealthUrl within 24 seconds"
}

Write-Host "[install] world save = $WorldFile"
Write-Host "[install] logs = $stdoutLog / $stderrLog"
Write-Host "[install] health = $HealthUrl"
