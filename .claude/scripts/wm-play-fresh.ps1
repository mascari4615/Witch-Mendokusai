<#
.SYNOPSIS
  Editor: (optional recompile) -> wait until the editor really finished -> Play with a fresh save -> wait for World.
  -Stop exits Play and restores productName.

.DESCRIPTION
  2026-11-17: Play requested right after a recompile was dropped silently several times
  (eval answered 1 while the domain reload was still pending). This waits on
  isCompiling/isUpdating twice and retries Play until it actually starts.
  Fresh save = productName changed for this Play only, so persistentDataPath is new
  (first-run path like a new phone install). -Stop puts productName back.

  ASCII only (PowerShell 5.1 reads BOM-less files as cp949).
#>
param(
  [string]$ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path,
  [switch]$Recompile,
  [switch]$Stop,
  [int]$TimeoutSec = 300
)
$ErrorActionPreference = 'Continue'
$probeDir = Join-Path $env:TEMP 'wm-play-fresh'
New-Item -ItemType Directory -Force $probeDir | Out-Null

$freshPlay = @'
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
if (EditorApplication.isPlaying) { EditorApplication.ExitPlaymode(); return "stopping"; }
PlayerSettings.productName = "WM Fresh Verify " + DateTime.Now.ToString("HHmmss");
EditorUtility.audioMasterMute = true;
EditorSceneManager.OpenScene("Assets/_WitchMendokusai/Scenes/Intro.unity");
EditorApplication.EnterPlaymode();
return "fresh";
'@
$enterWorld = @'
using UnityEditor;
using UnityEngine.SceneManagement;
using WitchMendokusai;
if (EditorApplication.isPlaying == false) return "not playing";
EditorUtility.audioMasterMute = true;
if (DungeonManager.Instance != null) return "WORLD ready scene=" + SceneManager.GetActiveScene().name;
if (LobbyManager.Instance != null && UnityEngine.Object.FindAnyObjectByType<DataManager>() is DataManager data && data.IsDataLoaded) { LobbyManager.Instance.StartGame(); return "StartGame called"; }
return "WAIT scene=" + SceneManager.GetActiveScene().name;
'@
$stopReset = @'
using UnityEditor;
if (EditorApplication.isPlayingOrWillChangePlaymode) { EditorApplication.ExitPlaymode(); return "exiting"; }
PlayerSettings.productName = "WitchMendokusai";
return "restored " + PlayerSettings.productName;
'@
foreach ($pair in @(@('fresh.cs', $freshPlay), @('enter.cs', $enterWorld), @('stop.cs', $stopReset))) {
  [IO.File]::WriteAllText((Join-Path $probeDir $pair[0]), $pair[1], [Text.UTF8Encoding]::new($false))
}

function Invoke-Probe([string]$name) { unity command script eval --file (Join-Path $probeDir $name) --project-path $ProjectPath 2>&1 | Out-String }
function Wait-Idle {
  for ($i = 0; $i -lt 60; $i++) {
    $r = unity command eval "return UnityEditor.EditorApplication.isCompiling || UnityEditor.EditorApplication.isUpdating ? 0 : 1;" --project-path $ProjectPath 2>&1 | Out-String
    if ($r -match '"?result"?:\s*1\b') { return $true }
    Start-Sleep 5
  }
  return $false
}

# Restore productName only after Play really ended. The quit-time save follows productName,
# so restoring it first writes the throwaway save over the real one (2026-11-24).
if ($Stop) {
  for ($i = 0; $i -lt 20; $i++) {
    if ((Invoke-Probe 'stop.cs') -match 'restored WitchMendokusai') { Write-Host 'stopped, productName restored'; exit 0 }
    Start-Sleep 2
  }
  Write-Host 'Play did not end, productName NOT restored. Run -Stop again'
  exit 1
}

$start = Get-Date
if ($Recompile) {
  unity command recompile --project-path $ProjectPath 2>&1 | Out-Null
  for ($i = 0; $i -lt 60; $i++) {
    Start-Sleep 5
    $s = unity command recompile_status --project-path $ProjectPath 2>&1 | Out-String
    if ($s -match 'completed|up_to_date|idle') { break }
  }
}
if (-not (Wait-Idle)) { Write-Host 'editor not ready'; exit 2 }
Start-Sleep 5
if (-not (Wait-Idle)) { Write-Host 'editor not ready after reload'; exit 2 }

for ($attempt = 1; $attempt -le 3; $attempt++) {
  Invoke-Probe 'fresh.cs' | Out-Null
  $deadline = (Get-Date).AddSeconds(30)
  $playing = $false
  while ((Get-Date) -lt $deadline) {
    Start-Sleep 5
    if ((Invoke-Probe 'enter.cs') -notmatch 'not playing') { $playing = $true; break }
  }
  if ($playing) { break }
  Write-Host "play did not start (attempt $attempt), retrying"
  if (-not (Wait-Idle)) { exit 2 }
}
while (((Get-Date) - $start).TotalSeconds -lt $TimeoutSec) {
  if ((Invoke-Probe 'enter.cs') -match 'WORLD ready') { Write-Host "WORLD ready after $([int]((Get-Date) - $start).TotalSeconds)s"; exit 0 }
  Start-Sleep 6
}
Write-Host 'timed out before World'
exit 1
