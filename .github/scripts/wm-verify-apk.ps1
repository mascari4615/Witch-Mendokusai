# Verify a built Android APK before it reaches a phone. ASCII only (PS 5.1 reads BOM-less files as cp949).
#
# Checks
#   1. every zip entry decompresses (a 25.6 MB zero-filled hole on 2026-11-17 made the phone refuse install:
#      INSTALL_PARSE_FAILED_NO_CERTIFICATES)
#   2. the Addressables catalog points at this platform's folder (2026-11-17 the APK catalog pointed at
#      aa/StandaloneWindows64 while bundles sat in aa/Android, so every data load failed on the phone)
#
# Usage: wm-verify-apk.ps1 -Apk <path> [-PlatformFolder Android]
# Exit 0 pass, 1 fail.
param(
    [Parameter(Mandatory = $true)][string]$Apk,
    [string]$PlatformFolder = 'Android'
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$failures = New-Object System.Collections.Generic.List[string]
$archive = [IO.Compression.ZipFile]::OpenRead($Apk)
try {
    $buffer = New-Object byte[] 1048576
    $entries = 0
    $broken = 0
    $catalogBytes = $null
    $settingsBytes = $null
    foreach ($entry in $archive.Entries) {
        $entries++
        try {
            $stream = $entry.Open()
            $memory = $null
            if ($entry.FullName -eq 'assets/aa/catalog.bin' -or $entry.FullName -eq 'assets/aa/catalog.json' -or $entry.FullName -eq 'assets/aa/settings.json') {
                $memory = New-Object IO.MemoryStream
            }
            while (($read = $stream.Read($buffer, 0, $buffer.Length)) -gt 0) {
                if ($memory) { $memory.Write($buffer, 0, $read) }
            }
            $stream.Dispose()
            if ($memory) {
                if ($entry.FullName -eq 'assets/aa/settings.json') { $settingsBytes = $memory.ToArray() } else { $catalogBytes = $memory.ToArray() }
            }
        } catch {
            $broken++
            if ($broken -le 5) { $failures.Add("broken entry: $($entry.FullName)") }
        }
    }
    if ($broken -gt 0) { $failures.Add("broken entries: $broken of $entries") }

    $bundleFolders = @($archive.Entries | Where-Object { $_.FullName -like 'assets/aa/*/*.bundle' } |
        ForEach-Object { ($_.FullName -split '/')[2] } | Sort-Object -Unique)
    if ($bundleFolders.Count -gt 0 -and ($bundleFolders -notcontains $PlatformFolder)) {
        $failures.Add("bundle folders are [$($bundleFolders -join ',')], expected $PlatformFolder")
    }

    if ($catalogBytes) {
        # catalog.bin splits paths into deduplicated segments, so search the bare folder name.
        # Only long, unambiguous names (short ones like iOS can appear in unrelated strings).
        $texts = @([Text.Encoding]::UTF8.GetString($catalogBytes), [Text.Encoding]::Unicode.GetString($catalogBytes))
        if ($settingsBytes) { $texts += [Text.Encoding]::UTF8.GetString($settingsBytes) }
        foreach ($other in @('StandaloneWindows64', 'StandaloneWindows', 'StandaloneOSX', 'StandaloneLinux64')) {
            if ($other -eq $PlatformFolder) { continue }
            if (@($texts | Where-Object { $_.Contains($other) }).Count -gt 0) {
                $failures.Add("catalog references another platform folder: $other")
                break
            }
        }
    } elseif ($bundleFolders.Count -gt 0) {
        $failures.Add('addressables bundles present but no assets/aa/catalog.bin or catalog.json')
    }
    Write-Host "[verify-apk] entries=$entries broken=$broken bundleFolders=[$($bundleFolders -join ',')] catalog=$([bool]$catalogBytes)"
} finally {
    $archive.Dispose()
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Host "::error::[verify-apk] $_" }
    exit 1
}
Write-Host '[verify-apk] PASS'
exit 0
