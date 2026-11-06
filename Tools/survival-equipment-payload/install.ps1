# 임시 묶음을 Assets 로 복사. 대상 checkout 루트에서 실행. Unity 임포트로 .meta 생성.
param([Parameter(Mandatory = $true)][string]$ProjectRoot)

$ErrorActionPreference = 'Stop'
$source = Split-Path -Parent $MyInvocation.MyCommand.Path
$editorTarget = Join-Path $ProjectRoot 'Assets/_WitchMendokusai/Editor/Survival'
New-Item -ItemType Directory -Force $editorTarget | Out-Null
Copy-Item (Join-Path $source 'SurvivalEquipmentBuilder.cs') $editorTarget -Force
Copy-Item (Join-Path $source 'SurvivalEquipmentBuilder.Prefabs.cs') $editorTarget -Force
Copy-Item (Join-Path $source 'SurvivalEquipmentBuilder.Specs.cs') $editorTarget -Force
Copy-Item (Join-Path $source 'SurvivalWeaponProbe.cs') $editorTarget -Force
Copy-Item (Join-Path $source 'ScaleOverLife.cs') (Join-Path $ProjectRoot 'Assets/_WitchMendokusai/Domain/Crafting/Equipment/Weapon/Parts') -Force
Write-Output 'copied'
