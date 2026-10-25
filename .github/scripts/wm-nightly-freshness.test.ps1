# Successful-build marker scheduling; no Unity, network or real build directories.
# Run: powershell -NoProfile -ExecutionPolicy Bypass -File .github/scripts/wm-nightly-freshness.test.ps1
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\wm-build-card.ps1"
# Resolve the directory itself: TEMP may end in a separator, unlike Parent.FullName.
$temporaryParent = (Get-Item -LiteralPath (Join-Path $env:TEMP '.') -Force).FullName
$fixtureName = 'wm-nightly-test-' + [guid]::NewGuid().ToString('N')
$root = (New-Item -ItemType Directory -Path (Join-Path $temporaryParent $fixtureName)).FullName
$script:now = [datetime]::Parse('2026-10-26T00:00:00Z').ToUniversalTime()
function Get-Date { return $script:now }
$passed = 0
function Check {
    param([string]$Name, [bool]$Actual, [bool]$Expected)
    if ($Actual -ne $Expected) { throw "$Name expected=$Expected actual=$Actual" }
    $script:passed++
    Write-Host "PASS $Name"
}
function Set-MarkerAge {
    param([string]$Platform, [double]$Hours)
    (Get-Item (Get-LastBuiltMarker -BuildRoot $root -Platform $Platform)).LastWriteTimeUtc = $script:now.AddHours(-$Hours)
}
try {
    Check 'missing marker builds' (Test-BuildNeeded -BuildRoot $root -Platform windows -Sha current -MaxAgeHours 20) $true
    Save-BuiltMarker -BuildRoot $root -Platform windows -Sha current
    Check 'success writer records SHA' ((Get-Content (Get-LastBuiltMarker -BuildRoot $root -Platform windows) -Raw).Trim() -eq 'current') $true
    Set-MarkerAge windows 19.999
    Check 'same SHA fresh skips' (Test-BuildNeeded -BuildRoot $root -Platform windows -Sha current -MaxAgeHours 20) $false
    Check 'different SHA fresh builds' (Test-BuildNeeded -BuildRoot $root -Platform windows -Sha newer -MaxAgeHours 20) $true
    Set-MarkerAge windows 20
    Check 'exact age limit builds' (Test-BuildNeeded -BuildRoot $root -Platform windows -Sha current -MaxAgeHours 20) $true
    Set-MarkerAge windows 70.9
    Check 'same SHA stale builds' (Test-BuildNeeded -BuildRoot $root -Platform windows -Sha current -MaxAgeHours 20) $true
    Check 'default caller still skips stale SHA' (Test-BuildNeeded -BuildRoot $root -Platform windows -Sha current) $false
    Check 'default caller still builds new SHA' (Test-BuildNeeded -BuildRoot $root -Platform windows -Sha newer) $true
    Set-MarkerAge windows -1
    Check 'future marker builds' (Test-BuildNeeded -BuildRoot $root -Platform windows -Sha current -MaxAgeHours 20) $true
    Save-BuiltMarker -BuildRoot $root -Platform android -Sha current
    Set-MarkerAge android 70.9
    Check 'Android default unchanged' (Test-BuildNeeded -BuildRoot $root -Platform android -Sha current) $false
    Check 'Android new SHA builds' (Test-BuildNeeded -BuildRoot $root -Platform android -Sha newer) $true
    # Execute the actual workflow gate body against only the temporary markers.
    $workflow = Get-Content "$PSScriptRoot\..\workflows\build.yml" -Raw
    $gate = [regex]::Match($workflow, '(?s)- name: Nightly gate.*?run: \|\r?\n(?<body>.*?)(?=\r?\n      # TASK-WM-201)')
    if (-not $gate.Success) { throw 'Nightly gate body missing' }
    Check 'gate remains schedule-only' ($gate.Value.Contains("if: github.event_name == 'schedule'")) $true
    $savedEnvironment = @{}
    foreach ($name in @('GITHUB_WORKSPACE', 'BUILD_ROOT', 'GITHUB_SHA', 'GITHUB_OUTPUT')) {
        $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name)
    }
    try {
        $env:GITHUB_WORKSPACE = (Resolve-Path "$PSScriptRoot\..\..").Path
        $env:BUILD_ROOT = $root
        $env:GITHUB_SHA = 'current'
        foreach ($platform in @('windows', 'android')) {
            $env:GITHUB_OUTPUT = Join-Path $root "$platform-output"
            Set-MarkerAge $platform 70.9
            $body = $gate.Groups['body'].Value.Replace('${{ steps.cfg.outputs.platform }}', $platform)
            Invoke-Expression $body
            Check "$platform workflow stale skip output" (Test-Path $env:GITHUB_OUTPUT) ($platform -eq 'android')
        }
    } finally {
        foreach ($name in $savedEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name]) }
    }
    Write-Host "$passed / $passed PASS"
} finally {
    $target = Get-Item -LiteralPath $root -Force
    if ($target.FullName -ne [IO.Path]::GetFullPath($root) -or
        $target.Parent.FullName -ne $temporaryParent -or $target.Name -ne $fixtureName -or
        -not $target.PSIsContainer -or ($target.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw 'Refusing cleanup outside the generated temporary fixture directory'
    }
    Remove-Item -LiteralPath $root -Recurse -Force
}
