function Set-WmPlayerFirewall {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][string]$Program)

    $root = [IO.Path]::GetFullPath('C:\wm-builds').TrimEnd('\')
    $path = [IO.Path]::GetFullPath($Program)
    if (-not $path.StartsWith($root + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Player must be inside the build output root.'
    }
    $file = Get-Item -LiteralPath $path -ErrorAction Stop
    if ($file.Name -notin @('WitchMendokusai.exe', 'WM.exe') -or
        $file.Directory.Parent.FullName -ne $root -or
        $file.Directory.Name -notlike '*-windows' -or
        -not (Test-Path -LiteralPath (Join-Path $file.Directory.FullName ($file.BaseName + '_Data')) -PathType Container)) {
        throw 'Expected a Windows player with its data directory in a build folder.'
    }
    foreach ($item in @($file, $file.Directory, (Get-Item -LiteralPath $root))) {
        if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Linked player paths are not supported.' }
    }

    $hasher = [Security.Cryptography.SHA256]::Create()
    try { $key = ([BitConverter]::ToString($hasher.ComputeHash([Text.Encoding]::UTF8.GetBytes($path.ToLowerInvariant())))).Replace('-', '').Substring(0, 20) }
    finally { $hasher.Dispose() }

    foreach ($policy in @(
        @{Suffix='Private'; Profile='Private'; Action='Allow'},
        @{Suffix='Public'; Profile='Public'; Action='Block'}
    )) {
        $name = 'WM-Player-' + $key + '-' + $policy.Suffix
        $existing = Get-NetFirewallRule -Name $name -ErrorAction SilentlyContinue
        if ($existing) {
            Set-NetFirewallRule -Name $name -Program $path -Direction Inbound -Profile $policy.Profile -Action $policy.Action -Enabled True -ErrorAction Stop | Out-Null
        } else {
            New-NetFirewallRule -Name $name -DisplayName ('WM player ' + $policy.Suffix + ' ' + $file.Directory.Name) -Group 'WM build players' -Program $path -Direction Inbound -Profile $policy.Profile -Action $policy.Action -Enabled True -ErrorAction Stop | Out-Null
        }
        $rule = Get-NetFirewallRule -Name $name -PolicyStore ActiveStore -ErrorAction Stop
        $app = $rule | Get-NetFirewallApplicationFilter -ErrorAction Stop
        if ([string]$rule.Enabled -ne 'True' -or [string]$rule.Direction -ne 'Inbound' -or
            [string]$rule.Profile -ne $policy.Profile -or [string]$rule.Action -ne $policy.Action -or
            -not [string]::Equals($app.Program, $path, [StringComparison]::OrdinalIgnoreCase)) {
            throw "Player firewall policy did not become effective: $name"
        }
        Write-Host "Player firewall: $($policy.Profile) $($policy.Action) $path"
    }
}
