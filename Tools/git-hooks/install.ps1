# Tools/git-hooks/install.ps1 — TASK-WM-109-F
#
# Installs the WM post-commit hook into the shared .git/hooks/ dir for the
# main checkout (and, by extension, every linked worktree — git uses the
# common dir for hooks). Run from any WM checkout / worktree:
#
#   powershell -File Tools/git-hooks/install.ps1
#
# Re-runnable (no-op if already installed). Variants:
#   -Force      overwrite existing hook
#   -Uninstall  remove the hook

[CmdletBinding()]
param(
    [switch]$Uninstall,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

$repoRoot = (git rev-parse --show-toplevel 2>$null).Trim()
if ([string]::IsNullOrWhiteSpace($repoRoot))
{
    Write-Host "[wm-hooks] not in a git repo — abort"
    exit 1
}

$commonDir = (git rev-parse --git-common-dir 2>$null).Trim()
if (-not [System.IO.Path]::IsPathRooted($commonDir))
{
    $commonDir = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $commonDir))
}

$hooksDir = Join-Path $commonDir 'hooks'
$hookSource = Join-Path $repoRoot 'Tools\git-hooks\post-commit'
$hookTarget = Join-Path $hooksDir 'post-commit'

# pre-push = 「내가 안 만진 줄이 왜 바뀌었나」 관문 (TASK-WM-205/207).
# 그동안 `wm-rule-gate.ps1` 머리말은 이 seam 이 있다고 적어뒀는데 아무도 안 깔고 있었다.
$prePushSource = Join-Path $repoRoot 'Tools\git-hooks\pre-push'
$prePushTarget = Join-Path $hooksDir 'pre-push'

if ($Uninstall)
{
    foreach ($target in @($hookTarget, $prePushTarget))
    {
        if (Test-Path -LiteralPath $target)
        {
            Remove-Item -LiteralPath $target -Force
            Write-Host "[wm-hooks] removed: $target"
        }
        else
        {
            Write-Host "[wm-hooks] nothing to remove at $target"
        }
    }
    exit 0
}

if (-not (Test-Path -LiteralPath $hookSource))
{
    Write-Host "ERROR: hook source missing: $hookSource"
    Write-Host "       (run from inside a WitchMendokusai checkout)"
    exit 1
}

if (-not (Test-Path -LiteralPath $hooksDir))
{
    New-Item -ItemType Directory -Path $hooksDir -Force | Out-Null
}

if ((Test-Path -LiteralPath $hookTarget) -and -not $Force)
{
    # Detect whether already pointing at our script (idempotent).
    $existing = Get-Content -LiteralPath $hookTarget -Raw -ErrorAction SilentlyContinue
    if ($existing -match 'wm-commit-verify\.ps1')
    {
        Write-Host "[wm-hooks] already installed: $hookTarget"
        Write-Host "[wm-hooks]   ledger: $(Join-Path $commonDir 'wm-commit-log.tsv')"
        exit 0
    }
    Write-Host "[wm-hooks] foreign hook already at $hookTarget"
    Write-Host "[wm-hooks]   overwrite with -Force, or remove first"
    exit 2
}

Copy-Item -LiteralPath $hookSource -Destination $hookTarget -Force

# Try to mark executable via Git for Windows' bash (chmod). Best-effort —
# Windows NTFS will treat shell hooks as executable so long as `sh` can find
# them; the +x bit is mostly a UX nicety + cross-OS portability.
$bash = Get-Command bash -ErrorAction SilentlyContinue
if ($null -ne $bash)
{
    $hookPosix = ($hookTarget -replace '\\', '/')
    & $bash.Source -c "chmod +x '$hookPosix'" 2>$null | Out-Null
}

Write-Host "[wm-hooks] installed: $hookTarget"

# --- pre-push (되돌림 감지) — 없으면 조용히 넘어간다(구 체크아웃 호환). ---
if (Test-Path -LiteralPath $prePushSource)
{
    $installPrePush = $true
    if ((Test-Path -LiteralPath $prePushTarget) -and -not $Force)
    {
        $existingPrePush = Get-Content -LiteralPath $prePushTarget -Raw -ErrorAction SilentlyContinue
        if ($existingPrePush -notmatch 'wm-revert-audit')
        {
            Write-Host "[wm-hooks] foreign pre-push already at $prePushTarget — 건너뜀 (-Force 로 덮어쓰기)"
            $installPrePush = $false
        }
    }
    if ($installPrePush)
    {
        Copy-Item -LiteralPath $prePushSource -Destination $prePushTarget -Force
        if ($null -ne $bash)
        {
            $prePushPosix = ($prePushTarget -replace '\', '/')
            & $bash.Source -c "chmod +x '$prePushPosix'" 2>$null | Out-Null
        }
        Write-Host "[wm-hooks] installed: $prePushTarget"
    }
}

Write-Host "[wm-hooks]   ledger:  $(Join-Path $commonDir 'wm-commit-log.tsv')"
Write-Host "[wm-hooks]   docs:    Tools/git-hooks/README.md"
