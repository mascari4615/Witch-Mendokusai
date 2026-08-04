# 워크플로 `run:` 블록 회귀 검사 — 노트북(PS 5.1)에서 죽는 블록을 미리 잡는다 (TASK-WM-197).
#
# 사고: GitHub Actions 는 `run:` 블록을 **BOM 없는 임시 .ps1** 로 떨구고 PowerShell 5.1 은
# 그걸 cp949 로 읽는다. `run:` 안에 한글이 있으면 글자가 깨지고, 깨진 바이트가 따옴표로
# 변하는 순간 스크립트 전체가 파싱 실패한다. run #12 에서 빌드가 성공하고도 알림 단계가
# 죽은 원인이 정확히 이것이다.
#
# 이 검사는 그 상황을 그대로 재현한다:
#   ① powershell 셸 블록만 고른다 (bash 블록을 PS 로 파싱하면 당연히 틀린다)
#   ② Actions 가 먼저 치환하는 ${{ }} 표현식을 더미 값으로 바꾼다
#   ③ BOM 없이 저장 → PS 파서에 통과시킨다
#
# 실행: powershell -ExecutionPolicy Bypass -File .github/scripts/check-workflow-ascii.ps1

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$workflowDir = Join-Path $repoRoot '.github\workflows'

function Test-RunBlock {
    param([string[]]$Body, [int]$StartLine, [string]$File)
    if ($Body.Count -eq 0) { return 0 }
    # Actions 는 표현식을 *먼저* 치환한다. 치환 전 원문을 파싱하면 없는 문제를 만든다.
    $text = ($Body -join "`n") -replace '\$\{\{[^}]*\}\}', 'X'
    $temp = Join-Path $env:TEMP ('wf-check-' + [Guid]::NewGuid().ToString('N') + '.ps1')
    [System.IO.File]::WriteAllText($temp, $text, (New-Object System.Text.UTF8Encoding($false)))
    $errs = $null
    $tokens = $null
    [System.Management.Automation.Language.Parser]::ParseFile($temp, [ref]$tokens, [ref]$errs) | Out-Null
    Remove-Item $temp -Force -ErrorAction SilentlyContinue
    if ($errs.Count -gt 0) {
        Write-Host "[FAIL] $File line $StartLine"
        $errs | Select-Object -First 3 | ForEach-Object { Write-Host "       $($_.Message)" }
        return 1
    }
    return 0
}

$failures = 0
foreach ($workflow in Get-ChildItem $workflowDir -Filter *.yml) {
    $lines = Get-Content $workflow.FullName
    $index = 0
    $blockIndent = -1
    $block = @()
    $blockStart = 0
    $isPowerShell = $false      # 이 step 의 shell
    $stepIsPowerShell = $false

    foreach ($line in $lines) {
        $index++

        if ($blockIndent -ge 0) {
            $indent = ($line -replace '^(\s*).*$', '$1').Length
            if ($line.Trim() -ne '' -and $indent -le $blockIndent) {
                if ($isPowerShell) { $failures += Test-RunBlock -Body $block -StartLine $blockStart -File $workflow.Name }
                $blockIndent = -1
                $block = @()
            } else {
                $block += $line
                continue
            }
        }

        # step 경계에서 shell 기억을 초기화 (다음 step 의 shell 을 물려받지 않게).
        if ($line -match '^\s*- name:') { $stepIsPowerShell = $false }
        if ($line -match "^\s*shell:\s*'?powershell") { $stepIsPowerShell = $true }
        if ($line -match '^\s*shell:\s*(bash|sh|cmd|pwsh)') { $stepIsPowerShell = $false }

        if ($line -match '^(\s*)run: \|') {
            $blockIndent = $Matches[1].Length
            $blockStart = $index
            $block = @()
            $isPowerShell = $stepIsPowerShell
        }
    }
    if ($blockIndent -ge 0 -and $isPowerShell) {
        $failures += Test-RunBlock -Body $block -StartLine $blockStart -File $workflow.Name
    }
}

if ($failures -gt 0) {
    Write-Host ''
    Write-Host "run 블록 $failures 개가 노트북(PS 5.1)에서 죽는다. 한글 텍스트는 .github/scripts/*.ps1 (BOM 유지) 로 옮겨라."
    exit 1
}
Write-Host 'OK - 모든 powershell run 블록이 BOM 없이도 파싱된다.'
