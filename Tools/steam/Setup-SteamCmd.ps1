<#
.SYNOPSIS
  SteamPipe 업로드 준비를 한 번에: steamcmd 설치, 첫 로그인, 계정 이름 저장.

.DESCRIPTION
  사람이 대화형 PowerShell 창에서 직접 실행하는 스크립트 (비밀번호와 Steam Guard 코드는 사람이 입력).
  1. steamcmd 가 없으면 Valve 공식 주소에서 받아 %USERPROFILE%\.karmoddrine\steamcmd 에 풂
  2. steamcmd 첫 실행 (자체 업데이트, 수십 초)
  3. steamcmd +login <계정> +quit. 이 창에서 비밀번호와 Steam Guard 코드 입력. 성공하면 로그인 정보가 이 PC 에 저장됨
  4. 계정 이름을 사용자 환경 변수 STEAM_BUILDER_ACCOUNT 에 저장 (비밀번호는 저장 안 함)
  다시 실행해도 안전 (이미 있는 단계는 건너뜀). 로그인이 만료되면 이 스크립트를 다시 실행.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File Tools\steam\Setup-SteamCmd.ps1
#>
[CmdletBinding()]
param(
    [string]$Account = $env:STEAM_BUILDER_ACCOUNT,
    [string]$InstallDir = (Join-Path $env:USERPROFILE '.karmoddrine\steamcmd')
)

$ErrorActionPreference = 'Stop'

# Valve 공식 배포 주소 (Steamworks 문서 SteamCMD 절)
$STEAMCMD_URL = 'https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip'
$steamCmd = Join-Path $InstallDir 'steamcmd.exe'

Write-Host ''
Write-Host '== 1/4 steamcmd 설치 =='
if (Test-Path -LiteralPath $steamCmd)
{
    Write-Host "이미 있음: $steamCmd"
}
else
{
    New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
    $zipPath = Join-Path $InstallDir 'steamcmd.zip'
    Write-Host "받는 중: $STEAMCMD_URL"
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri $STEAMCMD_URL -OutFile $zipPath -UseBasicParsing
    Expand-Archive -LiteralPath $zipPath -DestinationPath $InstallDir -Force
    Remove-Item -LiteralPath $zipPath
    # 실행 전에 Valve 서명 확인. 아니면 지우고 중단
    $exeSignature = Get-AuthenticodeSignature -LiteralPath $steamCmd
    if ($exeSignature.Status -ne 'Valid' -or $exeSignature.SignerCertificate.Subject -notmatch 'Valve')
    {
        Remove-Item -LiteralPath $steamCmd -Force
        throw "steamcmd.exe 서명이 Valve 가 아님 ($($exeSignature.Status)). 지우고 중단: $InstallDir"
    }
    Write-Host "설치 완료, 서명 확인: $($exeSignature.SignerCertificate.Subject)"
}

Write-Host ''
Write-Host '== 2/4 steamcmd 첫 실행 (자체 업데이트, 수십 초) =='
& $steamCmd +quit
Write-Host "끝 (종료 코드 $LASTEXITCODE, 첫 실행은 0 이 아니어도 정상)"

Write-Host ''
Write-Host '== 3/4 Steam 로그인 =='
if ([string]::IsNullOrWhiteSpace($Account))
{
    $Account = Read-Host 'Steamworks 권한이 있는 Steam 계정 이름 (로그인 ID, 이메일 아님)'
}
if ([string]::IsNullOrWhiteSpace($Account)) { throw '계정 이름이 비어 있음' }
Write-Host "계정 $Account 로 로그인. 이어서 비밀번호, 그다음 Steam Guard 코드 (또는 휴대폰 앱 승인) 를 이 창에 입력"
& $steamCmd +login $Account +quit

Write-Host ''
Write-Host '== 4/4 저장된 로그인 확인 =='
# 비밀번호 없이 다시 붙어 봄. 저장이 안 됐으면 입력을 기다리지 않고 실패
$ErrorActionPreference = 'Continue'
$check = & $steamCmd +@NoPromptForPassword 1 +login $Account +quit 2>&1 | Out-String
$ErrorActionPreference = 'Stop'
if ($check -match 'Logging in user .*\.\.\.OK|Waiting for user info\.\.\.OK')
{
    [Environment]::SetEnvironmentVariable('STEAM_BUILDER_ACCOUNT', $Account, 'User')
    Write-Host "준비 완료. STEAM_BUILDER_ACCOUNT=$Account 저장 (새 창부터 적용)"
    Write-Host '다음: powershell -ExecutionPolicy Bypass -File Tools\steam\Upload-SteamBuild.ps1 -BuildZip <받은 zip> -Preview'
}
else
{
    Write-Host $check
    throw '저장된 로그인으로 다시 붙지 못함. 3단계 출력 확인 뒤 이 스크립트를 다시 실행'
}
