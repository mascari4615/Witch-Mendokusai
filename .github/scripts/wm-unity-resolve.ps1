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

    # IL2CPP(win64) 는 **윈도우 빌드에만** 필요하다. 플랫폼과 무관하게 요구하면, 그 모듈이
    # 없는 기계에서 안드로이드를 구울 때마다 쓰지도 않을 모듈을 받으러 간다.
    $needsWinIl2cpp = ($Platform -ne 'android')
    if ((Test-Path $exe) -eq $false -or ($needsWinIl2cpp -and (Test-Path $il2cpp) -eq $false)) {
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
        if ($needsWinIl2cpp -and (Test-Path $il2cpp) -eq $false) {
            throw "Unity $version 은 설치됐는데 IL2CPP(win64) 모듈이 없다. WM 은 IL2CPP 백엔드라 필수."
        }
        Write-Host "Unity $version 자동 설치 완료."
    }

    # ★ 안드로이드 모듈 확인은 **에디터를 확보한 뒤**에 한다. 앞에 두면, 에디터가 아예 없는
    #   기계에서 「Android 모듈이 없다」고 말한다 — 진짜 문제(에디터 없음)를 가리고 사람을
    #   엉뚱한 곳으로 보낸다. 없을 때 정확히 말하는 것이 이 검사의 존재 이유다.
    #
    # ★ 2026-09-01 사용자 결정: **없으면 스스로 받는다.** 전에는 "10GB 라 사람이 Hub 로 한 번"
    #   이었는데, 그러면 에디터 버전을 올릴 때마다 사람이 노트북에 붙어야 한다. 이 파일 머리말이
    #   에디터에 대해 말한 것과 같은 이유로 모듈도 자동.
    #   첫 회는 20~40분 추가, 그 뒤로는 있는 것 사용.
    if ($Platform -eq 'android') {
        $androidPlayer = "$root\Editor\Data\PlaybackEngines\AndroidPlayer"
        if ((Test-Path $androidPlayer) -eq $false) {
            if ([string]::IsNullOrEmpty($changeset)) {
                throw "Unity $version 에 Android 모듈이 없다 ($androidPlayer). ProjectVersion.txt 에 changeset 이 없어 자동 설치 불가."
            }

            $hub = 'C:\Program Files\Unity Hub\Unity Hub.exe'
            if ((Test-Path $hub) -eq $false) { throw "Unity Hub 없음: $hub" }

            Write-Host "Android 모듈 없음 - Hub headless 설치 시작 (SDK/NDK/JDK 포함 약 10GB, 첫 회 20~40분)."
            $moduleLog = Join-Path $InstallLogDir "unity-android-$version.log"

            # --childModules 가 SDK/NDK/JDK 를 딸려 온다. 셋을 따로 적으면 Hub 판에 따라
            # 이름이 갈려 조용히 하나가 빠진다 (그러면 빌드가 SDK 없음으로 죽는다).
            $moduleArgs = @('--', '--headless', 'install-modules',
                '--version', $version, '--module', 'android', '--childModules')
            $moduleRun = Start-Process -FilePath $hub -ArgumentList $moduleArgs `
                -RedirectStandardOutput $moduleLog -RedirectStandardError "$moduleLog.err" -PassThru -Wait -WindowStyle Hidden
            Write-Host "Hub install-modules exit=$($moduleRun.ExitCode) (log: $moduleLog)"

            if ((Test-Path $androidPlayer) -eq $false) {
                if (Test-Path $moduleLog) { Get-Content $moduleLog -Tail 40 | Write-Host }
                if (Test-Path "$moduleLog.err") { Get-Content "$moduleLog.err" -Tail 20 | Write-Host }
                throw "Android 모듈 자동 설치 실패 ($androidPlayer 가 안 생겼다)."
            }

            Write-Host "Android 모듈 자동 설치 완료."
        }
        Write-Host "Android 모듈 확인됨."
    }

    Write-Host "Unity $version -> $exe"
    return @{ Version = $version; Exe = $exe }
}
