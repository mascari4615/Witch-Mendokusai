<#
.SYNOPSIS
  Windows 빌드 폴더 하나를 SteamPipe 로 올린다 (WitchMendokusai depot).

.DESCRIPTION
  - App ID 정본은 Assets/_WitchMendokusai/Core/Resources/SETTINGS_0_Steam.asset (게임 코드와 같은 값)
  - depot 번호, exe 이름, 제외 목록은 같은 폴더 steampipe.json
  - app_build vdf 는 매번 임시 폴더에 새로 쓴다. 저장소에 생성물이 남지 않는다
  - 로그인: steamcmd 의 저장된 로그인 정보 사용. 비밀번호와 Steam Guard 코드는 이 스크립트가 받지 않는다.
    이 기계에서 처음이면 대화형 터미널에서 직접 `steamcmd +login <계정> +quit` 한 번 (코드 입력은 사람)
  - 첫 업로드는 -Preview 로. BuildOutput 의 manifest 에서 디버그 폴더 (Burst, IL2CPP) 제외가 먹었는지 확인 (폴더 패턴 미검증)
  - 기본은 올리기만. 브랜치 공개 (-SetLive) 는 default 가 아닌 이름만 허용. default 공개는 Steamworks 웹에서 사람이

.EXAMPLE
  ./Tools/steam/Upload-SteamBuild.ps1 -BuildZip $HOME\Downloads\WitchMendokusai-123.zip -Preview
  ./Tools/steam/Upload-SteamBuild.ps1 -BuildDir C:\wm-builds\123\out -DryRun
  ./Tools/steam/Upload-SteamBuild.ps1 -BuildDir C:\wm-builds\123\out -Account karmo_builder -Preview
  ./Tools/steam/Upload-SteamBuild.ps1 -BuildDir C:\wm-builds\123\out -Account karmo_builder -SetLive beta
#>
[CmdletBinding()]
param(
    [string]$BuildDir = '',
    [string]$BuildZip = '',
    [string]$Account = $env:STEAM_BUILDER_ACCOUNT,
    [string]$SteamCmd = $env:STEAMCMD,
    [string]$Description = '',
    [string]$SetLive = '',
    [switch]$Preview,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$settingsAsset = Join-Path $repoRoot 'Assets\_WitchMendokusai\Core\Resources\SETTINGS_0_Steam.asset'
$configPath = Join-Path $PSScriptRoot 'steampipe.json'

function Get-SteamAppId([string]$assetPath)
{
    if (-not (Test-Path -LiteralPath $assetPath)) { throw "설정 자산 없음: $assetPath" }
    $line = Select-String -LiteralPath $assetPath -Pattern '^\s*appId:\s*(\d+)\s*$' | Select-Object -First 1
    if (-not $line) { throw "appId 줄을 못 찾음: $assetPath" }
    $id = [uint32]$line.Matches[0].Groups[1].Value
    if ($id -eq 0 -or $id -eq 480) { throw "App ID 가 비었거나 시험용 480: $id" }
    return $id
}

function ConvertTo-VdfString([string]$value)
{
    # steamcmd 의 vdf 는 역슬래시를 이스케이프로 읽지 않음 (Valve 예시 경로가 홑 역슬래시).
    # 따옴표만 홑따옴표로 바꿔 문자열이 끊기지 않게
    return '"' + ($value -replace '"', "'") + '"'
}

# --- 입력 검사 -----------------------------------------------------------------
$config = Get-Content -LiteralPath $configPath -Raw -Encoding UTF8 | ConvertFrom-Json
$appId = Get-SteamAppId $settingsAsset
$depotId = [uint32]$config.depotId
if ($depotId -eq 0) { throw "steampipe.json depotId 비어 있음" }

if (($BuildDir -eq '') -eq ($BuildZip -eq '')) { throw "-BuildDir (빌드 폴더) 나 -BuildZip (Files 빌드 탭에서 받은 zip) 중 하나만" }

$sourceLabel = ''
if ($BuildZip -ne '')
{
    # zip 은 임시 폴더에 풀고, exe 가 든 폴더를 내용 뿌리로 (zip 안 맨 위 폴더 유무와 무관)
    if (-not (Test-Path -LiteralPath $BuildZip -PathType Leaf)) { throw "zip 없음: $BuildZip" }
    $extractDir = Join-Path $env:TEMP ('wm-steampipe\content-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
    Write-Host "[steampipe] zip 푸는 중: $BuildZip"
    Expand-Archive -LiteralPath $BuildZip -DestinationPath $extractDir -Force
    $found = @(Get-ChildItem -LiteralPath $extractDir -Recurse -File -Filter $config.exe)
    if ($found.Count -ne 1) { throw "zip 안 $($config.exe) 가 1개가 아님 ($($found.Count)개): $BuildZip" }
    $BuildDir = $found[0].DirectoryName
    $sourceLabel = [System.IO.Path]::GetFileNameWithoutExtension($BuildZip)
}

if (-not (Test-Path -LiteralPath $BuildDir -PathType Container)) { throw "빌드 폴더 없음: $BuildDir" }
$contentRoot = (Resolve-Path -LiteralPath $BuildDir).Path.TrimEnd('\')
$exePath = Join-Path $contentRoot $config.exe
if (-not (Test-Path -LiteralPath $exePath)) { throw "빌드 폴더에 $($config.exe) 없음: $contentRoot" }

if ($SetLive -ne '')
{
    if ($SetLive -eq 'default') { throw "default 브랜치 공개는 Steamworks 웹에서 사람이 한다. -SetLive 는 beta 같은 이름만" }
    if ($SetLive -notmatch '^[A-Za-z0-9_-]+$') { throw "브랜치 이름 형식 이상: $SetLive" }
}

if ($Description -eq '')
{
    # PS 5.1 은 Stop 상태에서 네이티브 stderr 한 줄도 종료로 바꿈. git 경고 (dubious ownership 등) 로 죽지 않게 잠시 낮춤
    $ErrorActionPreference = 'Continue'
    $sha = (& git -C $repoRoot rev-parse --short HEAD 2>$null)
    $ErrorActionPreference = 'Stop'
    if ([string]::IsNullOrWhiteSpace($sha)) { $sha = 'nogit' }
    $stamp = (Get-Item -LiteralPath $exePath).LastWriteTime.ToString('yyyy-MM-dd HH:mm')
    $Description = "WM $sha exe $stamp"
    if ($sourceLabel -ne '') { $Description = "WM $sourceLabel exe $stamp" }
}

# --- vdf 쓰기 ------------------------------------------------------------------
$workDir = Join-Path $env:TEMP ('wm-steampipe\' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
# BuildOutput 은 실행마다 같은 자리. steamcmd 의 로컬 chunk 캐시를 다음 업로드가 재사용
$outputDir = Join-Path $env:TEMP 'wm-steampipe\output'
New-Item -ItemType Directory -Force -Path $workDir, $outputDir | Out-Null

$exclusions = ($config.exclude | ForEach-Object { "`t`t`t`"FileExclusion`" " + (ConvertTo-VdfString $_) }) -join "`r`n"
$previewFlag = if ($Preview) { '1' } else { '0' }

$vdf = @"
"AppBuild"
{
	"AppID" "$appId"
	"Desc" $(ConvertTo-VdfString $Description)
	"Preview" "$previewFlag"
	"SetLive" $(ConvertTo-VdfString $SetLive)
	"ContentRoot" $(ConvertTo-VdfString $contentRoot)
	"BuildOutput" $(ConvertTo-VdfString $outputDir)
	"Depots"
	{
		"$depotId"
		{
			"FileMapping"
			{
				"LocalPath" "*"
				"DepotPath" "."
				"recursive" "1"
			}
$exclusions
		}
	}
}
"@

$vdfPath = Join-Path $workDir "app_build_$appId.vdf"
# steamcmd 는 BOM 없는 파일을 기대
[System.IO.File]::WriteAllText($vdfPath, $vdf, (New-Object System.Text.UTF8Encoding($false)))

$fileCount = (Get-ChildItem -LiteralPath $contentRoot -Recurse -File | Measure-Object).Count
Write-Host "[steampipe] 앱 $appId depot $depotId, 파일 $fileCount 개, preview=$previewFlag, setlive='$SetLive'"
Write-Host "[steampipe] vdf: $vdfPath"

if ($DryRun)
{
    Write-Host "[steampipe] DryRun: steamcmd 를 부르지 않음"
    Write-Output $vdfPath
    return
}

# --- steamcmd 실행 -------------------------------------------------------------
if ([string]::IsNullOrWhiteSpace($Account)) { throw "-Account 나 환경 변수 STEAM_BUILDER_ACCOUNT 필요" }
if ([string]::IsNullOrWhiteSpace($SteamCmd))
{
    $SteamCmd = Join-Path $env:USERPROFILE '.karmoddrine\steamcmd\steamcmd.exe'
}
if (-not (Test-Path -LiteralPath $SteamCmd))
{
    throw "steamcmd 없음: $SteamCmd. Steamworks SDK 의 tools\ContentBuilder\builder\steamcmd.exe 를 두거나 -SteamCmd 로 지정"
}

$logPath = Join-Path $workDir 'steamcmd.log'
# +@NoPromptForPassword 1: 저장된 로그인이 만료됐을 때 입력을 기다리며 멈추지 않고 바로 실패
# stderr 경고로 스크립트가 끊기지 않게 호출 동안만 Continue
$ErrorActionPreference = 'Continue'
& $SteamCmd +@NoPromptForPassword 1 +login $Account +run_app_build $vdfPath +quit 2>&1 | Tee-Object -FilePath $logPath
$exitCode = $LASTEXITCODE
$ErrorActionPreference = 'Stop'

# 판정은 성공 문구 기준. steamcmd 는 자체 업데이트 직후 성공해도 0 이 아닌 종료 코드를 내는 경우가 있음
$succeeded = Select-String -LiteralPath $logPath -Pattern 'Successfully finished AppID' -Quiet
if (-not $succeeded)
{
    throw "[steampipe] 업로드 실패 (exit $exitCode, 성공 문구 없음). 로그인 만료면 대화형으로 steamcmd +login 다시. 로그: $logPath"
}
if ($exitCode -ne 0)
{
    Write-Warning "[steampipe] 성공 문구는 있으나 종료 코드 $exitCode. 로그 확인: $logPath"
}

$buildId = Select-String -LiteralPath $logPath -Pattern 'BuildID\s+(\d+)' | Select-Object -Last 1
if ($buildId) { Write-Host "[steampipe] 완료 BuildID $($buildId.Matches[0].Groups[1].Value)" }
Write-Host "[steampipe] 로그: $logPath"
