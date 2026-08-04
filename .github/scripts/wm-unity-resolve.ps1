# 프로젝트가 요구하는 유니티 에디터를 찾고, 없으면 스스로 설치한다 (TASK-WM-197).
#
# 에디터 버전을 올릴 때마다 사람이 노트북에 붙어야 하면 그건 빌드머신이 아니다.
# 필요한 버전과 changeset 은 ProjectVersion.txt 가 이미 들고 있으므로 그대로 쓴다.
#
# ★ 이 파일은 **UTF-8 BOM 유지 필수** — 워크플로 `run:` 블록은 BOM 없이 떨어져 PS 5.1 이
#   cp949 로 읽는 탓에 한글이 깨지고 파싱까지 실패한다. 그래서 한글 로직은 여기 둔다.
#   (자세한 사고 기록은 wm-build-card.ps1 머리말)

function Resolve-UnityEditor {
    param(
        [string]$ProjectVersionFile = 'ProjectSettings/ProjectVersion.txt',
        [string]$Platform,
        [string]$InstallLogDir
    )
    $lines = Get-Content $ProjectVersionFile
    $version = (($lines | Where-Object { $_ -like 'm_EditorVersion:*' } | Select-Object -First 1) -split ':\s*', 2)[1].Trim()
    $revLine = ($lines | Where-Object { $_ -like 'm_EditorVersionWithRevision:*' } | Select-Object -First 1)
    $changeset = ''
    if ($revLine -match '\(([0-9a-f]+)\)') { $changeset = $Matches[1] }

    $root = "C:\Program Files\Unity\Hub\Editor\$version"
    $exe = "$root\Editor\Unity.exe"
    $il2cpp = "$root\Editor\Data\PlaybackEngines\windowsstandalonesupport\Variations\win64_player_nondevelopment_il2cpp"

    # 안드로이드는 별도 모듈(SDK/NDK/JDK 포함, 약 10GB)이 필요하다. 없으면 여기서 멈춘다 —
    # 빌드 도중 알 수 없는 실패로 새는 것보다 낫다. 설치는 Hub 로 한 번 해두면 유지된다.
    if ($Platform -eq 'android') {
        $androidPlayer = "$root\Editor\Data\PlaybackEngines\AndroidPlayer"
        if ((Test-Path $androidPlayer) -eq $false) {
            throw "Unity $version 에 Android 모듈이 없다 ($androidPlayer). Unity Hub 로 android + SDK/NDK/JDK 설치 필요."
        }
        Write-Host "Android 모듈 확인됨."
    }

    if ((Test-Path $exe) -eq $false -or (Test-Path $il2cpp) -eq $false) {
        if ([string]::IsNullOrEmpty($changeset)) {
            throw "Unity $version 없음. ProjectVersion.txt 에 changeset 이 없어 자동 설치 불가 — Unity Hub 로 직접 설치 필요."
        }
        $installed = (Get-ChildItem 'C:\Program Files\Unity\Hub\Editor' -ErrorAction SilentlyContinue).Name -join ', '
        Write-Host "Unity $version ($changeset) 없음 (설치된 것: [$installed]) — Hub headless 설치 시작. 수 GB 다운로드라 첫 회는 오래 걸린다."
        $hub = 'C:\Program Files\Unity Hub\Unity Hub.exe'
        if ((Test-Path $hub) -eq $false) { throw "Unity Hub 없음: $hub" }
        $installLog = Join-Path $InstallLogDir "unity-install-$version.log"
        $arguments = @('--', '--headless', 'install', '--version', $version, '--changeset', $changeset, '--module', 'windows-il2cpp', '--childModules')
        $process = Start-Process -FilePath $hub -ArgumentList $arguments -RedirectStandardOutput $installLog -RedirectStandardError "$installLog.err" -PassThru -Wait -WindowStyle Hidden
        Write-Host "Hub install exit=$($process.ExitCode) (log: $installLog)"
        if ((Test-Path $exe) -eq $false) {
            if (Test-Path $installLog) { Get-Content $installLog -Tail 30 | Write-Host }
            throw "Unity $version 자동 설치 실패."
        }
        if ((Test-Path $il2cpp) -eq $false) {
            throw "Unity $version 은 설치됐는데 IL2CPP(win64) 모듈이 없다. WM 은 IL2CPP 백엔드라 필수."
        }
        Write-Host "Unity $version 자동 설치 완료."
    }

    Write-Host "Unity $version -> $exe"
    return @{ Version = $version; Exe = $exe }
}
