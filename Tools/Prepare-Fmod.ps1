param(
    [string]$Archive = $env:WM_FMOD_ARCHIVE
)

$ErrorActionPreference = 'Stop'
$packageName = 'com.fmod.fmod-for-unity.v204'
$version = '2.4.0'
$archiveHash = '4D4FF8080710AA4F781D516E7998EB36070996E9C69725E09573037E3F9D5600'
$root = Split-Path -Parent $PSScriptRoot
$package = Join-Path $root "Packages/$packageName"
$patch = Join-Path $PSScriptRoot 'fmod-unity7-serialized-guid.patch'
$patchHash = (Get-FileHash -LiteralPath $patch -Algorithm SHA256).Hash
$markerText = "$archiveHash $patchHash"
$marker = Join-Path $package '.wm-fmod-package'

if (Test-Path -LiteralPath $package) {
    if ((Test-Path -LiteralPath $marker -PathType Leaf) -and
        (Get-Content -LiteralPath $marker -Raw).Trim() -eq $markerText -and
        (Test-Path -LiteralPath (Join-Path $package 'FMODUnity.asmdef') -PathType Leaf)) {
        Write-Output "FMOD $version ready"
        exit 0
    }
    throw "Unexpected existing FMOD package at $package. Move it aside before preparing the locked package."
}

if (!$Archive) {
    $Archive = 'C:/wm-tool-cache/fmod/2.04.00/fmodstudio20400.tgz'
}
if (!(Test-Path -LiteralPath $Archive -PathType Leaf)) {
    throw 'FMOD archive missing. Set WM_FMOD_ARCHIVE to the official fmodstudio20400.tgz.'
}
$Archive = (Resolve-Path -LiteralPath $Archive).Path
if ((Get-FileHash -LiteralPath $Archive -Algorithm SHA256).Hash -ne $archiveHash) {
    throw 'Official FMOD archive SHA-256 mismatch.'
}

$temp = Join-Path ([IO.Path]::GetTempPath()) ('wm-fmod-' + [Guid]::NewGuid().ToString('N'))
New-Item -Path $temp -ItemType Directory | Out-Null
try {
    # Windows tar 를 직접 지정. git 훅처럼 PATH 앞에 Git 의 /usr/bin 이 오면 Git tar 가 잡히고, 그 tar 는 C:/ 경로를 원격 호스트로 읽어 실패
    if (!$env:SystemRoot) { throw 'SystemRoot missing: Windows tar.exe cannot be located.' }
    $tar = Join-Path $env:SystemRoot 'System32\tar.exe'
    if (!(Test-Path -LiteralPath $tar -PathType Leaf)) { throw "Windows tar.exe missing: $tar" }
    & $tar -xzf $Archive --strip-components 1 -C $temp
    if ($LASTEXITCODE -ne 0) { throw 'FMOD archive extraction failed.' }
    $identity = Get-Content -LiteralPath (Join-Path $temp 'package.json') -Raw | ConvertFrom-Json
    if ($identity.name -ne $packageName -or $identity.version -ne $version) {
        throw 'Unexpected FMOD package identity.'
    }
    & git -C $temp apply --unidiff-zero -p4 $patch
    if ($LASTEXITCODE -ne 0) { throw 'WM FMOD serialization patch failed.' }
    Set-Content -LiteralPath (Join-Path $temp '.wm-fmod-package') -Value $markerText -Encoding ascii
    Move-Item -LiteralPath $temp -Destination $package
    Write-Output "FMOD $version ready"
} finally {
    if (Test-Path -LiteralPath $temp) {
        $resolvedTemp = (Resolve-Path -LiteralPath $temp).Path
        $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\','/') + [IO.Path]::DirectorySeparatorChar
        if (!$resolvedTemp.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'FMOD staging directory escaped its temporary root.'
        }
        Remove-Item -LiteralPath $resolvedTemp -Recurse -Force
    }
}
