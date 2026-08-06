# Remove-OldBuilds retention policy - regression test.
#
# Why this exists: the prune step deletes build folders on the laptop, and it runs with
# if:always() - so it also runs right after a failed build. If the policy is wrong the
# damage is silent and unrecoverable: the pinned "latest build" card keeps pointing at a
# link whose file is gone, or the failure log you actually wanted to read is deleted.
# None of it had a test. This one creates fake build folders and runs the real function.
#
# ASCII only on purpose: PowerShell 5.1 reads BOM-less UTF-8 as the ANSI codepage and a
# Korean string then breaks the parser. This file keeps a BOM (repo lint checks for it)
# and still stays ASCII so it survives either way.
#
# Run: powershell -NoProfile -ExecutionPolicy Bypass -File .github/scripts/wm-prune-retention.test.ps1

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\wm-build-card.ps1"

$root = Join-Path $env:TEMP ("prune-test-" + [guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Path $root | Out-Null

function New-Build {
    param([string]$Name, [string]$Kind)
    $d = Join-Path $root $Name
    New-Item -ItemType Directory -Path $d | Out-Null
    if ($Kind -eq 'ok') { Set-Content (Join-Path $d 'game.apk') 'x' }
    # The name matters: the keep-failed rule looks for exactly this file, which is what
    # the build step writes. A log under any other name does not save the folder.
    if ($Kind -eq 'failed') { Set-Content (Join-Path $d 'unity-build.log') 'crash' }
    if ($Kind -eq 'oddlog') { Set-Content (Join-Path $d 'other.log') 'crash' }
    Start-Sleep -Milliseconds 30
    return $d
}

# Pinned target is created FIRST so it is also the oldest - age alone cannot explain
# its survival.
$pinned   = New-Build 'b00-pinned'      'ok'
$oldFail2 = New-Build 'b01-failed-old2' 'failed'
$oldFail1 = New-Build 'b02-failed-old1' 'failed'
$empty    = New-Build 'b03-empty'       'empty'
$oddLog   = New-Build 'b04-oddlog'      'oddlog'
$ok1      = New-Build 'b05-ok'          'ok'
$fail3    = New-Build 'b06-failed'      'failed'
$fail2    = New-Build 'b07-failed'      'failed'
$fail1    = New-Build 'b08-failed'      'failed'
$ok2      = New-Build 'b09-ok'          'ok'

$marker = Join-Path $root 'latest-success-android.json'
Set-Content $marker '{"link":"https://laptop.example/dl/b00-pinned/game.apk?exp=1"}' -Encoding ascii

$removed = Remove-OldBuilds -BuildRoot $root -Keep 5 -KeepFailed 3

Write-Host ("survivors: " + (((Get-ChildItem $root -Directory | Sort-Object Name).Name) -join ', '))
Write-Host ("removed  : " + ($removed -join ', '))

$failures = 0
function Check {
    param([string]$What, [bool]$Ok)
    if ($Ok) { Write-Host "  OK   $What" } else { Write-Host "  FAIL $What"; $script:failures++ }
}

Check 'builds with artifacts survive' ((Test-Path $ok1) -and (Test-Path $ok2))
Check 'pinned card target survives even as the oldest folder' (Test-Path $pinned)
Check '3 most recent failed builds keep their log' ((Test-Path $fail1) -and (Test-Path $fail2) -and (Test-Path $fail3))
Check 'older failed builds are pruned' ((-not (Test-Path $oldFail1)) -and (-not (Test-Path $oldFail2)))
Check 'empty shell is pruned' (-not (Test-Path $empty))
Check 'folder whose log has another name is NOT protected' (-not (Test-Path $oddLog))

Remove-Item $root -Recurse -Force
if ($failures -gt 0) { Write-Host "FAILURES: $failures"; exit 1 }
Write-Host 'ALL PASS'
