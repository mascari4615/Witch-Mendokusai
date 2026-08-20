# wm-runner-hygiene.ps1 — 배치 러너가 안 쓰는 것을 유니티가 굽지 않게 한다.
#
# ★ 왜 (2026-08-20 실측): 야간 빌드가 08-17 부터 접근 위반(0xC0000005)으로 죽었다.
#   크래시 자리는 애드레서블 직후 `BuildReport::BeginBuildStep` = 큰 할당 지점.
#   러너 체크아웃의 `Library\Search` 가 **파일 하나에 24.8GB** 였다.
#   `UserSettings\Search.index` 가 roots=[Assets,Packages] · properties · dependencies 로
#   깔려 있어서, 유니티가 뜰 때마다 이 인덱스를 다시 굽고 메모리에 올린다.
#   실패한 판은 전부 pagefile peak 2150MB > 할당 2048MB (= 커밋 한계를 쳤다),
#   성공한 판은 peak 568MB 였다.
#
#   같은 `Search.index` 설정을 쓰는 데스크톱 체크아웃은 `Library\Search` 가 **54MB** 다.
#   러너만 24,864MB — 460배. 설정이 이상한 게 아니라 러너 인덱스가 폭주한 것이다
#   (러너는 하루에도 여러 번 배치로 프로젝트를 열고, 그때마다 인덱스가 붙는다).
#
#   배치 러너는 Quick Search 를 **쓸 일이 없다**. 사람이 안 보는 창을 위해
#   24GB 를 굽다가 빌드를 죽이고 있었다. 그래서 러너에서는 인덱스를 두지 않는다.
#
#   사람 기계는 안 건드린다 — 이 스크립트는 CI 스텝에서만 부른다.

param([Parameter(Mandatory = $true)][string]$ProjectPath)

$ErrorActionPreference = 'Stop'

$searchIndex = Join-Path $ProjectPath 'UserSettings\Search.index'
$searchCache = Join-Path $ProjectPath 'Library\Search'

if (Test-Path $searchIndex) {
    Remove-Item $searchIndex -Force
    Write-Host '[HYGIENE] removed UserSettings/Search.index (batch runner does not use Quick Search)'
}

if (Test-Path $searchCache) {
    $mb = [math]::Round((Get-ChildItem $searchCache -Recurse -File -EA SilentlyContinue |
        Measure-Object Length -Sum).Sum / 1MB, 1)
    Remove-Item $searchCache -Recurse -Force -EA SilentlyContinue
    Write-Host "[HYGIENE] removed Library/Search ($mb MB)"
}

$os = Get-CimInstance Win32_OperatingSystem
Write-Host ("[HYGIENE] commit {0}/{1} GB free" -f
    [math]::Round($os.FreeVirtualMemory / 1MB, 1), [math]::Round($os.TotalVirtualMemorySize / 1MB, 1))
