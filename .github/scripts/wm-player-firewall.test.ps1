$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'wm-player-firewall.ps1')
$script:rules = @{}
$script:created = 0
$script:dataExists = $true
$script:linked = $false
$script:wrongEffectivePath = $false
function Get-Item {
    param($LiteralPath, $ErrorAction)
    $directory = [IO.Path]::GetDirectoryName($LiteralPath)
    [pscustomobject]@{
        Name=[IO.Path]::GetFileName($LiteralPath)
        BaseName=[IO.Path]::GetFileNameWithoutExtension($LiteralPath)
        Attributes=$(if ($script:linked) { [IO.FileAttributes]::ReparsePoint } else { [IO.FileAttributes]::Normal })
        Directory=[pscustomobject]@{FullName=$directory;Name=[IO.Path]::GetFileName($directory);Attributes=0;Parent=[pscustomobject]@{FullName=[IO.Path]::GetDirectoryName($directory)}}
    }
}
function Test-Path { param($LiteralPath,$PathType) return $script:dataExists }
function Get-NetFirewallRule { param($Name,$ErrorAction,$PolicyStore) return $script:rules[$Name] }
function New-NetFirewallRule {
    param($Name,$DisplayName,$Group,$Program,$Direction,$Profile,$Action,$Enabled,$ErrorAction)
    $script:created++
    $script:rules[$Name] = [pscustomobject]@{Name=$Name;Program=$Program;Direction=$Direction;Profile=$Profile;Action=$Action;Enabled=$Enabled}
}
function Set-NetFirewallRule {
    param($Name,$Program,$Direction,$Profile,$Action,$Enabled,$ErrorAction)
    $script:rules[$Name].Program=$Program
    $script:rules[$Name].Direction=$Direction
    $script:rules[$Name].Profile=$Profile
    $script:rules[$Name].Action=$Action
    $script:rules[$Name].Enabled=$Enabled
}
function Get-NetFirewallApplicationFilter {
    param([Parameter(ValueFromPipeline=$true)]$InputObject)
    process { [pscustomobject]@{Program=$(if ($script:wrongEffectivePath) {'C:\wrong.exe'} else {$InputObject.Program})} }
}
$script:passed=0
function Reject {
    param($Name,[scriptblock]$Action)
    $rejected=$false
    try { & $Action } catch { $rejected=$true }
    if (-not $rejected) { throw "FAIL $Name" }
    $script:passed++
    Write-Host "PASS $Name"
}
Reject 'outside build root' { Set-WmPlayerFirewall -Program 'C:\other\WM.exe' }
Reject 'prefix lookalike root' { Set-WmPlayerFirewall -Program 'C:\wm-builds-other\run-windows\WM.exe' }
Reject 'crash handler' { Set-WmPlayerFirewall -Program 'C:\wm-builds\run-windows\UnityCrashHandler64.exe' }
Reject 'nested directory' { Set-WmPlayerFirewall -Program 'C:\wm-builds\run-windows\nested\WM.exe' }
$script:dataExists=$false
Reject 'missing player data' { Set-WmPlayerFirewall -Program 'C:\wm-builds\run-windows\WM.exe' }
$script:dataExists=$true
$script:linked=$true
Reject 'linked executable' { Set-WmPlayerFirewall -Program 'C:\wm-builds\run-windows\WM.exe' }
$script:linked=$false
Set-WmPlayerFirewall -Program 'C:\wm-builds\run-windows\WM.exe'
Set-WmPlayerFirewall -Program 'C:\wm-builds\run-windows\WM.exe'
if ($script:created -ne 2 -or $script:rules.Count -ne 2) { throw 'Duplicate policy on repeated configuration' }
$private=@($script:rules.Values | Where-Object Profile -eq Private)
$public=@($script:rules.Values | Where-Object Profile -eq Public)
if ($private.Count -ne 1 -or $private[0].Action -ne 'Allow' -or $public.Count -ne 1 -or $public[0].Action -ne 'Block') { throw 'Unexpected network scope' }
$script:passed++
Write-Host 'PASS private allow, public block and repeated configuration'
$script:wrongEffectivePath=$true
Reject 'effective policy mismatch fails' { Set-WmPlayerFirewall -Program 'C:\wm-builds\run-windows\WM.exe' }
Write-Host "cases=$script:passed failed=0"
