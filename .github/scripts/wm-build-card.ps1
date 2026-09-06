# WM 빌드 실행·보고 — 워크플로의 실제 살림 (TASK-WM-197).
#
# ★ 왜 로직이 여기 있나 (워크플로 안이 아니라):
#   GitHub Actions 는 `run:` 블록을 **BOM 없는 임시 .ps1** 로 떨구고, PowerShell 5.1 은
#   그걸 cp949 로 읽는다. 그래서 `run:` 안에 한글이 있으면 글자가 깨지고, 깨진 바이트가
#   따옴표로 변하는 순간 스크립트가 통째로 파싱 실패한다 (실측: run #12 의 '버전' 한 단어가
#   빌드 성공 후 알림 단계를 죽였다). 워크플로에는 ASCII 배선만 두고, 한글이 필요한 로직은
#   BOM 이 있는 이 파일에 모은다.
#   → 이 파일은 **UTF-8 BOM 유지 필수**. BOM 이 날아가면 같은 사고가 재발한다.
#
# 구성: 빌드 실행(진행 카드 갱신 포함) / 결과 보고 / 남은 프로세스 정리.

$script:LaptopOpsUri = 'http://127.0.0.1:47615'

# 「멈춤 의심」 기준 — 단계에 따라 다르다.
#
# 실측: 네이티브 변환 구간은 유니티 로그가 15분 넘게 한 줄도 안 늘지만, 그동안 Library
# 밑에서는 오브젝트 파일이 계속 쓰인다(=정상 작동). 반면 앞 단계(임포트·컴파일·번들)는
# 로그가 꾸준히 늘어서, 거기서 10분 침묵은 진짜 이상 신호다.
# 그래서 하나의 숫자로 뭉뚱그리지 않는다 — 뭉뚱그리면 오경보를 내거나(짧게 잡으면)
# 정작 멈춰도 30분을 버린다(길게 잡으면).
#
# 산출물 쪽을 직접 감시하는 건 비용이 크다: .o 는 깊은 하위 폴더에 쓰여 상위 디렉토리
# mtime 이 안 바뀌고(실측), 전체 재귀 스캔은 폴링마다 하기엔 무겁다.
$script:SilenceWarnMinutesEarly = 10   # 로그가 계속 늘어야 정상인 앞 단계
$script:SilenceWarnMinutesNative = 45  # 로그가 원래 조용한 네이티브·Gradle 구간

function Get-SilenceThreshold {
    param([string]$StageKey)
    if ($StageKey -in @('native', 'project', 'gradle')) { return $script:SilenceWarnMinutesNative }
    return $script:SilenceWarnMinutesEarly
}

# ★ PS 5.1 의 Invoke-RestMethod 는 *문자열* 본문을 UTF-8 로 보내지 않는다 — 비-ASCII 가
#   전송 중에 '?' 로 뭉개진다. 실측: 첫 진행 카드가 디스코드에 「? WM ?? android」로
#   저장됐다(응답 원문 바이트로 확인). 그래서 본문은 반드시 *바이트* 로 보낸다.
#   파일 인코딩(BOM)과는 별개의 두 번째 관문이라, 하나만 고치면 여전히 깨진다.
function Invoke-LaptopOpsJson {
    param([string]$Path, [string]$Token, [hashtable]$Payload, [int]$TimeoutSec = 20)
    $json = $Payload | ConvertTo-Json -Depth 6 -Compress
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($json)
    return Invoke-RestMethod -Uri "$script:LaptopOpsUri$Path" -Method Post `
        -Headers @{ Authorization = "Bearer $Token" } `
        -ContentType 'application/json; charset=utf-8' -Body $bytes -TimeoutSec $TimeoutSec
}

function Get-LaptopOpsToken {
    # 노트북에서 토큰 정본은 laptop-ops .env. (데스크톱 관례 ~/.laptop-ops-token 은 폴백)
    $envFile = 'C:\Users\masca\repos\karmoddrine\memo\laptop-ops\.env'
    if (Test-Path $envFile) {
        $line = Get-Content $envFile | Where-Object { $_ -match '^\s*LAPTOP_OPS_TOKEN\s*=' } | Select-Object -First 1
        if ($line) { return (($line -split '=', 2)[1]).Trim().Trim('"').Trim("'") }
    }
    $tokenPath = 'C:\Users\masca\.laptop-ops-token'
    if (Test-Path $tokenPath) { return (Get-Content $tokenPath -Raw).Trim() }
    return $null
}

# 취소된 빌드의 카드를 치운다.
#
# 왜 지우나: 취소는 *사람이 의도한 중단* 이라 기록 가치가 낮은데, 채널 최상단을 차지하면
# 「빌드가 다 실패했다」로 읽힌다 (실측: 성공 3회 뒤 취소 시험 3회를 돌렸더니 사용자가
# 「하나도 성공 안 한 것 같다」고 했다). 결과가 없는 사건은 흔적도 남기지 않는 게 맞다.
# 대신 고정된 「최신 빌드」 카드의 *마지막 시도* 줄에 취소 사실이 남는다.
function Remove-BuildCard {
    param([string]$Token, [string]$MessageId)
    try {
        Invoke-LaptopOpsJson -Path '/notify' -Token $Token -Payload @{ channel = 'build'; messageId = $MessageId; delete = $true } | Out-Null
        return $true
    } catch {
        Write-Warning "build card delete failed: $($_.Exception.Message)"
        return $false
    }
}

# 카드 게시 → 메시지 id 반환 (이후 갱신의 손잡이). 실패해도 $null 만 돌려준다:
# 알림이 빌드 성패를 뒤집지 않는다.
function New-BuildCard {
    param([string]$Token, [hashtable]$Rich)
    try {
        $response = Invoke-LaptopOpsJson -Path '/notify' -Token $Token -Payload @{ channel = 'build'; rich = $Rich; wait = $true }
        return $response.messageId
    } catch {
        Write-Warning "build card post failed: $($_.Exception.Message)"
        return $null
    }
}

function Set-BuildCard {
    param([string]$Token, [string]$MessageId, [hashtable]$Rich)
    try {
        Invoke-LaptopOpsJson -Path '/notify' -Token $Token -Payload @{ channel = 'build'; rich = $Rich; messageId = $MessageId } | Out-Null
        return $true
    } catch {
        Write-Warning "build card update failed: $($_.Exception.Message)"
        return $false
    }
}

# 단계 사다리 — *실제 완료된 빌드 로그* 에서 뽑은 표식만 쓴다 (추측 금지).
#
# ★ 플랫폼마다 칸 수가 다르다. 안드로이드에만 나오는 단계(SDK 점검·Gradle 패키징)를
#   윈도우 사다리에 남겨두면 막대가 영원히 8칸을 못 채우고 「6/8 에서 멈춘 빌드」처럼
#   보인다 — 진행 표시가 거짓말을 하느니 칸을 줄이는 게 낫다.
#
# 검증 상태: 안드로이드 = 완료 로그(run #11)의 등장 순서로 실측. 윈도우 = 안드로이드와
#   공통인 앞 단계만 남겼고, 윈도우 전용 표식은 *아직 실측 못 했다*(보관 정책상 윈도우
#   로그가 남아 있지 않음). 그래서 윈도우는 공통 표식만으로 굴리고, 추측 표식은 넣지
#   않았다. 윈도우 빌드를 한 번 돌리면 그 로그로 칸을 더 정밀하게 나눌 수 있다.
function Get-BuildStageLadder {
    param([string]$Platform)
    $ladder = @(
        @{ Key = 'open';    Label = '프로젝트 여는 중';      Patterns = @('Refreshing native plugins', 'Initialize engine version') },
        @{ Key = 'compile'; Label = '스크립트 컴파일';       Patterns = @('DisplayProgressbar: Compiling Scripts') },
        @{ Key = 'prepare'; Label = '빌드 준비';             Patterns = @('Switch To Build Platform', 'Build Player Scripts', 'Processing Addressable Group') },
        @{ Key = 'bundle';  Label = '에셋 번들 굽기';        Patterns = @('Write Serialized Files', 'Archive And Compress Bundles', 'Post Processing Catalog Entries') }
    )
    if ($Platform -eq 'android') {
        $ladder += @{ Key = 'sdk';     Label = '안드로이드 도구 점검';  Patterns = @('Detecting Android SDK', 'Detect Android NDK', 'Check Android Player Settings') }
        $ladder += @{ Key = 'native';  Label = '네이티브 변환 (IL2CPP)'; Patterns = @('Fetching assembly references') }
        # 로그 순서 실측(run #11): collisions 41449 → Incremental Player Build 41459 →
        # IPostGenerate 41597 → Validate 41623 → Building Gradle 41626 → bee_backend 마지막
        # 41489 → Build Successful 46951. 즉 「collisions 가 보이면 곧 끝」이 아니다 —
        # 그 뒤로도 네이티브 컴파일이 십수 분 더 돈다. 「패키징」 한 단어로 뭉뚱그리면
        # 카드가 거의 다 된 것처럼 보이므로 두 칸으로 나눈다.
        $ladder += @{ Key = 'project'; Label = '안드로이드 프로젝트 생성'; Patterns = @('Check gradle project collisions', 'Incremental Player Build') }
        $ladder += @{ Key = 'gradle';  Label = 'Gradle 빌드·네이티브 마무리'; Patterns = @('Building Gradle project', 'Validate Gradle Project', 'IPostGenerateGradleAndroidProject') }
        $ladder += @{ Key = 'finish';  Label = '마무리·서명';           Patterns = @('Build Successful') }
    } else {
        $ladder += @{ Key = 'native';  Label = '네이티브 변환 (IL2CPP)'; Patterns = @('Fetching assembly references') }
        $ladder += @{ Key = 'finish';  Label = '마무리';                Patterns = @('Build Successful') }
    }
    return $ladder
}

# 새로 붙은 로그 조각에서 가장 앞선 단계를 찾는다. 되돌아가지 않는다(단조 증가) —
# 로그에는 옛 단어가 다시 나올 수 있는데 진행 막대가 뒤로 가면 그게 더 헷갈린다.
function Get-StageIndexFromChunk {
    param([string]$Chunk, [int]$CurrentIndex, [string]$Platform)
    $ladder = Get-BuildStageLadder -Platform $Platform
    $found = $CurrentIndex
    for ($i = $ladder.Count - 1; $i -ge 0; $i--) {
        if ($i -le $found) { break }
        foreach ($pattern in $ladder[$i].Patterns) {
            if ($Chunk.Contains($pattern)) { $found = $i; break }
        }
        if ($found -eq $i) { break }
    }
    return $found
}

function Get-ProgressBar {
    param([int]$Index, [int]$Total)
    $filled = [Math]::Max(0, [Math]::Min($Total, $Index + 1))
    return ('▰' * $filled) + ('▱' * ($Total - $filled))
}

# 진행 중 카드. 경과 시간을 같이 보여준다 — 「몇 분째인가」가 「어느 단계인가」만큼
# 궁금하고, 평소보다 오래 걸리는지도 이걸로 안다.
function New-ProgressRich {
    param([int]$StageIndex, [string]$Platform, [string]$BuildType, [string]$Commit,
        [datetime]$StartedAt, [string]$RunUrl, [string]$RunNumber, [int]$SilentMinutes = 0)
    $ladder = Get-BuildStageLadder -Platform $Platform
    $stage = $ladder[$StageIndex]
    $elapsed = [Math]::Round(((Get-Date) - $StartedAt).TotalMinutes, 1)
    $body = "$(Get-ProgressBar -Index $StageIndex -Total $ladder.Count)  $($StageIndex + 1)/$($ladder.Count) · **$($stage.Label)**"
    # 멈춤은 조용함에 숨는다 — 유니티가 걸려도 잡 타임아웃(4시간)까지 아무도 모른 채
    # 노트북이 잡혀 있게 된다. 그래서 *알리기만* 한다 (죽이지 않는다 — 오판으로 정상
    # 빌드를 끊는 게 더 나쁘다. 끊는 판단은 사람이 /빌드 취소 로).
    $stuck = $SilentMinutes -ge (Get-SilenceThreshold -StageKey $stage.Key)
    if ($stuck) {
        $body += "`n⚠️ 로그가 $SilentMinutes 분째 조용하다 — 멈췄을 수 있다 (실행 링크에서 확인)"
    }
    return @{
        title  = "⏳ WM 빌드 $Platform — 진행 중"
        body   = $body
        fields = @(
            @{ name = '플랫폼'; value = "$Platform / $BuildType"; inline = $true },
            @{ name = '경과';   value = "$elapsed 분"; inline = $true },
            @{ name = '커밋';   value = $Commit; inline = $true }
        )
        level  = $(if ($stuck) { 'warning' } else { 'progress' })
        url    = $RunUrl
        footer = "run #$RunNumber · 노트북 빌드머신"
    }
}

<#
.SYNOPSIS
유니티 배치 빌드를 돌리면서 진행 카드를 갱신한다.
.DESCRIPTION
유니티를 -Wait 없이 띄우고 로그의 *새로 붙은 부분만* 읽어 단계를 판정한다.
완성 로그가 7MB 까지 가므로 매번 통독하면 폴링이 빌드를 방해한다.
반환: @{ ExitCode; OutDir; OutFile; Report; UnityLog; CardId }
#>
function Invoke-WmBuild {
    param(
        [string]$UnityExe, [string]$Platform, [string]$BuildType, [string]$Version,
        [string]$BuildRoot, [string]$ProjectPath, [string]$RunNumber, [string]$Commit, [string]$RunUrl
    )
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $outDir = Join-Path $BuildRoot "$stamp-$RunNumber-$Platform"
    $outFile = Join-Path $outDir $(if ($Platform -eq 'android') { 'WitchMendokusai.apk' } else { 'WitchMendokusai.exe' })
    $report = Join-Path $outDir 'build-report.json'
    $unityLog = Join-Path $outDir 'unity-build.log'
    New-Item -ItemType Directory -Force -Path $outDir | Out-Null

    $arguments = @(
        '-quit', '-batchmode', '-nographics',
        '-projectPath', $ProjectPath,
        '-logFile', $unityLog,
        '-executeMethod', 'WitchMendokusai.EditorTools.WMBuilder.BuildFromCLI',
        '-wmOut', $outFile,
        '-wmReport', $report,
        '-wmTarget', $Platform,
        '-wmBuildNumber', $RunNumber,
        '-wmCommit', $Commit
    )
    if ($BuildType -eq 'development') { $arguments += '-wmDev' }
    if (-not [string]::IsNullOrWhiteSpace($Version)) { $arguments += @('-wmVersion', $Version) }

    $token = Get-LaptopOpsToken
    $startedAt = Get-Date
    $stageIndex = 0
    $cardId = $null
    if ($token) {
        $cardId = New-BuildCard -Token $token -Rich (New-ProgressRich -StageIndex 0 -Platform $Platform `
                -BuildType $BuildType -Commit $Commit -StartedAt $startedAt -RunUrl $RunUrl -RunNumber $RunNumber)
        # ★ 카드 손잡이는 *만들자마자* 넘긴다. 빌드가 끝난 뒤에 기록하면 취소 시 그 줄에
        #   도달하지 못해 손잡이를 잃고, 알림 step 이 빈 값을 받아 취소 카드를 *새 메시지로*
        #   또 만든다 (실측: run #15 에서 진행 카드와 취소 카드가 따로 남았다).
        if ($cardId -and $env:GITHUB_OUTPUT) {
            "card_id=$cardId" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8
        }
    }

    Write-Host "Unity args: $($arguments -join ' ')"
    $process = Start-Process -FilePath $UnityExe -ArgumentList $arguments -PassThru -WindowStyle Hidden

    $reader = $null
    $lastPost = Get-Date
    $lastStage = -1
    $lastGrowth = Get-Date       # 로그가 마지막으로 자란 시각 (멈춤 감지용)
    $silentMinutes = 0
    while ($process.HasExited -eq $false) {
        Start-Sleep -Seconds 20
        try {
            if (-not $reader -and (Test-Path $unityLog)) {
                # ReadWrite 공유로 열어 유니티 쓰기를 막지 않는다.
                $stream = [System.IO.File]::Open($unityLog, 'Open', 'Read', 'ReadWrite')
                $reader = New-Object System.IO.StreamReader($stream)
            }
            if ($reader) {
                $chunk = $reader.ReadToEnd()
                if ($chunk) {
                    $lastGrowth = Get-Date
                    $stageIndex = Get-StageIndexFromChunk -Chunk $chunk -CurrentIndex $stageIndex -Platform $Platform
                }
            }
        } catch {
            Write-Warning "log poll failed (ignored): $($_.Exception.Message)"
        }
        $silentMinutes = [int]((Get-Date) - $lastGrowth).TotalMinutes
        # 단계가 바뀌었거나 5분이 지났으면 갱신 (Discord rate limit 여유).
        if ($cardId -and (($stageIndex -ne $lastStage) -or (((Get-Date) - $lastPost).TotalMinutes -ge 5))) {
            $lastStage = $stageIndex
            $lastPost = Get-Date
            Set-BuildCard -Token $token -MessageId $cardId -Rich (New-ProgressRich -StageIndex $stageIndex -Platform $Platform `
                    -BuildType $BuildType -Commit $Commit -StartedAt $startedAt -RunUrl $RunUrl -RunNumber $RunNumber `
                    -SilentMinutes $silentMinutes) | Out-Null
        }
    }
    if ($reader) { $reader.Dispose() }

    return @{
        ExitCode = $process.ExitCode
        OutDir   = $outDir
        OutFile  = $outFile
        Report   = $report
        UnityLog = $unityLog
        CardId   = $cardId
    }
}

# 폰 설치 링크 — 단일 파일 산출물일 때만. 게이트웨이가 서명해 발급하므로 서명 비밀은
# 워크플로/GitHub secret 어디에도 없다 (노트북 .env 안에서만 산다).
# PC 빌드는 파일 하나가 아니라 exe + 데이터 폴더 통째다. 링크로 배달하려면 한 덩어리로 묶어야
# 한다 — 안 묶으면 「PC 빌드는 받을 방법이 없다」가 되고, 실제로 그랬다.
#
# ★ PS 5.1 의 Compress-Archive 는 2GB 를 넘는 입력에서 깨진다. WM 의 PC 빌드는 2.8GB 라
#   정면으로 걸린다 — 그래서 .NET 압축기를 직접 쓴다(zip64).
# ★ 압축률보다 속도(Fastest). 게임 에셋은 이미 압축돼 있어서 세게 조여봐야 크기는 조금 줄고
#   시간만 몇 배 든다.
# ★ 묶는 파일을 묶는 폴더 *안* 에 바로 만들면 자기 자신을 삼키려다 실패한다. 밖에서 만들어
#   다 된 뒤에 옮긴다.
# 폰 설치 안내 한 줄. 한글은 워크플로 run 블록에 못 둔다 (PS 5.1 이 비ASCII 를 깨뜨림, 린트 게이트). BOM 있는 이 파일에서만
function Get-PhoneInstallText {
    param([string]$Url)
    return "폰에 깔기: $Url"
}

function Get-BuildZip {
    param([string]$OutDir)
    $existing = Get-ChildItem $OutDir -File -Filter '*.zip' -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($existing) { return $existing }
    if (-not (Get-ChildItem $OutDir -File -Filter '*.exe' -ErrorAction SilentlyContinue)) { return $null }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $name = "WitchMendokusai-$(Split-Path $OutDir -Leaf).zip"
    $staging = Join-Path (Split-Path $OutDir -Parent) "$name.building"
    if (Test-Path $staging) { Remove-Item $staging -Force -ErrorAction SilentlyContinue }
    $sw = [Diagnostics.Stopwatch]::StartNew()
    [IO.Compression.ZipFile]::CreateFromDirectory($OutDir, $staging, [IO.Compression.CompressionLevel]::Fastest, $false)
    $final = Join-Path $OutDir $name
    Move-Item $staging $final -Force
    $sw.Stop()
    Write-Host "zip: $name ($([Math]::Round((Get-Item $final).Length / 1MB, 0)) MB, $([Math]::Round($sw.Elapsed.TotalMinutes, 1))분)"
    return Get-Item $final
}

# 링크가 언제까지 사는지는 *링크 자신* 이 안다. 「1일」처럼 글로 박아두면 정책을 바꾼 날
# 카드만 옛말을 한다 — 실제로 그랬다(1일 → 10일로 바꾼 뒤에도 카드는 「유효 1일」).
# 카드가 두 종류(개별 결과 / 고정 최신)라 문구도 두 벌이 되기 쉬워, 여기 한 곳에서만 만든다.
function Get-LinkExpiryText {
    param([string]$Link)
    if ($Link -match 'exp=(\d+)') {
        return [DateTimeOffset]::FromUnixTimeSeconds([int64]$Matches[1]).ToOffset([TimeSpan]::FromHours(9)).ToString('MM-dd HH:mm')
    }
    return ''
}

function Get-InstallLink {
    param([string]$Token, [string]$OutDir)
    if (-not $OutDir -or -not (Test-Path $OutDir)) { return $null }
    $artifact = Get-ChildItem $OutDir -File | Where-Object { $_.Extension -in '.apk', '.aab' } | Select-Object -First 1
    # 폰은 파일 하나가 곧 산출물, PC 는 폴더 통째 — 후자는 묶어서 같은 창구로 보낸다.
    if (-not $artifact) { $artifact = Get-BuildZip -OutDir $OutDir }
    if (-not $artifact) { return $null }
    try {
        # 지금은 경로가 ASCII 뿐이지만 같은 창구를 쓴다 — 예외를 두면 언젠가 그 예외로 샌다.
        # 기한은 일부러 안 보낸다 — 링크 수명은 정책이고, 정책은 게이트웨이 한 곳에서만
        # 정한다(`LAPTOP_OPS_DL_DEFAULT_DAYS`). 여기서 숫자를 박으면 정책을 바꿔도
        # 이 줄만 옛날 값으로 남아 조용히 어긋난다.
        $signed = Invoke-LaptopOpsJson -Path '/dl/sign' -Token $Token `
            -Payload @{ build = (Split-Path $OutDir -Leaf); file = $artifact.Name }
        return $signed.url
    } catch {
        Write-Warning "install link sign failed: $($_.Exception.Message)"
        return $null
    }
}

<#
.SYNOPSIS
빌드 결과를 카드로 마감한다 (진행 카드가 있으면 그 자리에서).
#>
function Publish-BuildResult {
    param(
        [string]$Status, [string]$Platform, [string]$BuildType, [string]$OutDir, [string]$Report,
        [string]$UnityLog, [string]$CardId, [string]$Commit, [string]$RefName, [string]$RunUrl,
        [string]$RunNumber, [string]$BuildRoot
    )
    $token = Get-LaptopOpsToken
    if (-not $token) { Write-Warning 'laptop-ops token not found - skip notify'; return }

    # 성공/실패/취소 3분기. 취소를 실패로 칠하면 「내가 껐다」와 「깨졌다」가 폰 화면에서
    # 구분이 안 된다 — 볼 이유가 다른 사건이라 색·문구를 나눈다.
    $ok = $Status -eq 'success'
    $cancelled = $Status -eq 'cancelled'
    if ($ok) { $icon = '🟢'; $level = 'info'; $statusWord = '성공' }
    elseif ($cancelled) { $icon = '⚪'; $level = 'warning'; $statusWord = '취소됨' }
    else { $icon = '🔴'; $level = 'error'; $statusWord = '실패' }

    $fields = @()
    $fields += @{ name = '플랫폼'; value = "$Platform / $BuildType"; inline = $true }
    if ($Report -and (Test-Path $Report)) {
        $json = Get-Content $Report -Raw | ConvertFrom-Json
        $artifactMb = [Math]::Round($json.exeSizeBytes / 1MB, 1)
        if ($artifactMb -le 0) { $artifactMb = [Math]::Round($json.totalSizeBytes / 1MB, 1) }
        $fields += @{ name = '버전'; value = "v$($json.version)"; inline = $true }
        $fields += @{ name = '크기'; value = "$artifactMb MB"; inline = $true }
        $fields += @{ name = '빌드 시간'; value = "$([Math]::Round($json.totalSeconds / 60, 1)) 분"; inline = $true }
        $fields += @{ name = '경고'; value = "$($json.warnings)"; inline = $true }
        # 조용히 쌓이는 것 — 성공해도 0이 아닐 수 있고, 아무도 안 보면 어느 날 빌드가 멈춘다.
        if ($json.errors -gt 0) {
            $fields += @{ name = '⚠ 리포트 에러'; value = "$($json.errors) 건 (로그 확인 권장)"; inline = $true }
        }
    }
    $fields += @{ name = '커밋'; value = "$Commit ($RefName)"; inline = $true }
    if ($OutDir) { $fields += @{ name = '노트북 경로'; value = $OutDir; inline = $false } }

    if ($BuildRoot) {
        try {
            $drive = Get-PSDrive -Name ($BuildRoot.Substring(0, 1)) -ErrorAction Stop
            $freeGb = [Math]::Round($drive.Free / 1GB, 1)
            if ($freeGb -lt 40) {
                $fields += @{ name = '⚠ 노트북 디스크'; value = "$freeGb GB 남음 (빌드 하나가 3GB 안팎)"; inline = $true }
            }
        } catch { }
    }

    # 실패면 *원인까지* 폰에 실어 보낸다. 로그 경로만 주면 결국 컴퓨터 앞으로 가야 하는데,
    # 그러면 알림이 「가서 봐라」 이상을 못 한다.
    if (-not $ok) {
        if ($UnityLog) { $fields += @{ name = '빌드 로그'; value = $UnityLog; inline = $false } }
        if ($cancelled) {
            $fields += @{ name = '무슨 일'; value = '사람이 중단시켰다. 남은 유니티는 정리했다.'; inline = $false }
        } elseif ($UnityLog -and (Test-Path $UnityLog)) {
            $lines = Get-Content $UnityLog -ErrorAction SilentlyContinue |
                Where-Object { $_ -match 'error CS|Exception:|Build Failed|error:' } |
                Select-Object -Last 6
            if ($lines) {
                # 임베드 필드는 1024자 상한 — 넘치면 Discord 가 메시지를 통째로 거부한다.
                $excerpt = ($lines -join "`n")
                if ($excerpt.Length -gt 900) { $excerpt = $excerpt.Substring($excerpt.Length - 900) }
                $fields += @{ name = '에러 (마지막 몇 줄)'; value = '```' + "`n$excerpt`n" + '```'; inline = $false }
            }
        }
    }

    # 폰에서 임베드 속 링크는 탭 표적이 작다 → 설치 링크만 임베드 밖 본문(lead)으로 빼서
    # 제목 크기로 렌더시킨다. 링크를 누르면 파일이 아니라 설치 페이지가 열린다.
    $link = $null
    if ($ok) { $link = Get-InstallLink -Token $token -OutDir $OutDir }
    $lead = $null
    if ($link) {
        # 폰은 눌러서 곧장 설치, PC 는 묶음을 받아 풀어서 실행 — 하는 일이 다르니 말도 달라야
        # 한다. 실측: PC 빌드 카드에 「폰에 설치하기」가 떠서 사용자가 이상하다고 했다.
        $expiry = Get-LinkExpiryText -Link $link
        $until = if ($expiry) { "$expiry KST 까지" } else { '기한 내' }
        if ($Platform -eq 'android') {
            $lead = "## [📲 폰에 설치하기]($link)"
            $bodyText = "$until · 처음 한 번만 비밀번호 (고정 메시지 참고)"
        }
        else {
            $lead = "## [💻 PC 빌드 받기 (zip)]($link)"
            $bodyText = "$until · 처음 한 번만 비밀번호 (고정 메시지 참고) · 받아서 풀고 실행"
        }
    } elseif ($ok) {
        $bodyText = '산출물은 노트북에만 있다 (받을 파일을 못 찾았다).'
    } elseif ($cancelled) {
        $bodyText = '중단된 빌드라 산출물이 없다. 다시 돌리려면 실행 링크에서 재실행.'
    } else {
        $bodyText = '빌드가 깨졌다 — 아래 에러부터 확인.'
    }

    $rich = @{
        lead   = $lead
        title  = "$icon WM 빌드 $Platform — $statusWord"
        body   = $bodyText
        fields = $fields
        level  = $level
        url    = $RunUrl
        footer = "run #$RunNumber · 노트북 빌드머신"
    }

    if ($cancelled) {
        # 취소는 결과가 없는 사건 — 카드를 남기면 채널 최상단이 「실패한 것들」처럼 보인다.
        # 흔적을 치우고, 사실은 고정 카드의 「마지막 시도」 줄로만 남긴다.
        if ($CardId) {
            $removed = Remove-BuildCard -Token $token -MessageId $CardId
            Write-Host "card removed ok=$removed id=$CardId (cancelled)"
        }
    } elseif ($CardId) {
        # 진행 카드가 있으면 *그 자리에서* 결과로 마감한다 — 진행하던 자리에 결과가 남는 게
        # 자연스럽고, 새 메시지를 또 쌓지 않는다. 못 고치면 그때만 새로 게시한다.
        $updated = Set-BuildCard -Token $token -MessageId $CardId -Rich $rich
        Write-Host "card closed ok=$updated id=$CardId link=$([bool]$link)"
        if (-not $updated) { New-BuildCard -Token $token -Rich $rich | Out-Null }
    } else {
        $posted = New-BuildCard -Token $token -Rich $rich
        Write-Host "card posted id=$posted link=$([bool]$link)"
    }

    # 고정 카드는 *항상* 갱신한다 — 성공/실패/취소 무관.
    # 단 받을 링크는 **마지막 성공** 것을 유지한다: 실패가 최신을 덮으면 어제 되던 것마저
    # 못 받게 된다. 「지금 상태」와 「받을 수 있는 것」은 다른 질문이라 한 카드에서 둘 다 답한다.
    Update-LatestCard -Token $token -BuildRoot $BuildRoot -Platform $Platform -OutDir $OutDir `
        -Report $Report -Link $link -Commit $Commit -RunUrl $RunUrl -StatusWord $statusWord -Icon $icon
}

<#
.SYNOPSIS
취소·실패 후 남은 유니티를 정리한다.
.DESCRIPTION
취소하면 runner 는 step 셸만 죽인다 — Start-Process 로 띄운 유니티는 살아남아 Library
락을 쥔 채 남고, 다음 빌드가 그 자리에서 막힌다. 이 워크스페이스를 가리키는 유니티만
골라 끝낸다 (노트북의 다른 유니티·에디터는 건드리지 않는다).
#>
function Stop-LeftoverUnity {
    param([string]$Workspace)
    $leftover = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" -ErrorAction SilentlyContinue |
        Where-Object { $_.CommandLine -and $_.CommandLine -like "*$Workspace*" }
    if (-not $leftover) { Write-Host 'no leftover unity'; return }
    foreach ($p in $leftover) {
        Write-Host "kill pid=$($p.ProcessId)"
        Stop-Process -Id $p.ProcessId -Force -ErrorAction SilentlyContinue
    }
    Start-Sleep -Seconds 2
    $still = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" -ErrorAction SilentlyContinue |
        Where-Object { $_.CommandLine -and $_.CommandLine -like "*$Workspace*" }
    if ($still) { Write-Warning "unity still alive: $(@($still).Count) - next build may block" }
    else { Write-Host 'cleanup done' }
}

<#
.SYNOPSIS
GitHub Actions 실행 요약(Step Summary)에 빌드 결과 표를 쓴다.
#>
function Write-BuildSummary {
    param([string]$Report, [string]$UnityLog, [string]$SummaryFile)
    $lines = @()
    if ($Report -and (Test-Path $Report)) {
        $json = Get-Content $Report -Raw | ConvertFrom-Json
        $sizeMb = [Math]::Round($json.exeSizeBytes / 1MB, 1)
        $totalMb = [Math]::Round($json.totalSizeBytes / 1MB, 1)
        $lines += "## 빌드 결과: $($json.result)"
        $lines += ''
        $lines += '| 항목 | 값 |'
        $lines += '| --- | --- |'
        $lines += "| 버전 | $($json.version) |"
        $lines += "| 개발 빌드 | $($json.development) |"
        $lines += "| 대상 | $($json.target) |"
        $lines += "| 산출물 크기 | $sizeMb MB |"
        $lines += "| 전체 크기 | $totalMb MB |"
        $lines += "| 소요 | $([Math]::Round($json.totalSeconds / 60, 1)) 분 |"
        $lines += "| 에러/경고 | $($json.errors) / $($json.warnings) |"
        $lines += "| 노트북 경로 | ``$($json.outPath)`` |"
    } else {
        # 리포트가 없다는 것 자체가 정보다 — 유니티가 리포트를 쓰기 전에 죽었다는 뜻.
        $lines += '## 빌드 결과: 리포트 없음 (Unity 가 리포트를 쓰기 전에 죽었다)'
        $lines += "노트북 로그: ``$UnityLog``"
    }
    $lines -join "`n" | Out-File -FilePath $SummaryFile -Append -Encoding utf8
}

# ── 야간 자동 빌드 ──────────────────────────────────────────────────────────
# 밤새 돌려두면 아침에 폰으로 바로 설치할 수 있다. 다만 *바뀐 게 없으면 굽지 않는다* —
# 같은 코드로 40분과 3GB 를 태우는 건 낭비고, 채널에 의미 없는 카드만 쌓인다.

# ── 오래된 빌드 정리 ───────────────────────────────────────────────────────
# 「최근 N개만 남긴다」는 순진한 규칙이 실제로 사고 직전까지 갔다.
#
# 취소·실패한 실행도 폴더는 만든다(산출물만 없다). 실측: 폴더 5개 중 4개가 빈 껍데기고
# 진짜 APK 는 하나뿐이었는데, 그 하나가 「최근 5개」의 맨 끝에 걸려 있었다. 빌드를 한 번만
# 더 취소하면 **고정 카드가 가리키는 유일한 산출물이 지워진다** — 카드는 멀쩡히 링크를
# 걸어두고 파일만 사라지는, 제일 나쁜 종류의 고장이다.
#
# 그래서 두 가지를 바꾼다:
#   ① 고정 카드가 가리키는 빌드는 **나이와 무관하게 못 지운다** (실제 링크 대조).
#   ② 빈 껍데기는 보관 정원을 못 차지한다 — 정원 N개는 *산출물이 든* 빌드에게만 준다.
function Remove-OldBuilds {
    param([string]$BuildRoot, [int]$Keep = 5, [string]$CurrentOutDir, [int]$KeepFailed = 3)
    if (-not (Test-Path $BuildRoot)) { return @() }

    # 고정 카드가 지금 가리키는 폴더들 — 링크 문자열에서 직접 뽑는다(별도 기록을 두면 어긋난다).
    $protected = New-Object System.Collections.Generic.HashSet[string] ([StringComparer]::OrdinalIgnoreCase)
    foreach ($marker in Get-ChildItem $BuildRoot -Filter 'latest-success-*.json' -ErrorAction SilentlyContinue) {
        try { $link = (Get-Content $marker.FullName -Raw | ConvertFrom-Json).link } catch { continue }
        if ($link -match '/dl/([^/]+)/') { [void]$protected.Add($Matches[1]) }
    }
    if ($CurrentOutDir) { [void]$protected.Add((Split-Path $CurrentOutDir -Leaf)) }

    $dirs = Get-ChildItem $BuildRoot -Directory -ErrorAction SilentlyContinue | Sort-Object CreationTime -Descending
    $kept = 0
    $keptFailed = 0
    $removed = @()
    foreach ($dir in $dirs) {
        if ($protected.Contains($dir.Name)) { continue }
        # zip 도 산출물로 센다 — PC 빌드는 묶음이 곧 배달물이다.
        $hasArtifact = @(Get-ChildItem $dir.FullName -Recurse -File -Include '*.apk', '*.aab', '*.exe', '*.zip' -ErrorAction SilentlyContinue).Count -gt 0
        if ($hasArtifact -and $kept -lt $Keep) { $kept++; continue }
        # ★ 실패한 빌드(산출물 없음)도 최근 몇 개는 남긴다 — **진단이 필요한 건 정확히 그것들이다.**
        #   실측: 같은 크래시가 2연속 났을 때 앞 회차 로그가 이미 지워져 두 로그를 대조하지
        #   못했다. 남는 건 로그 수 MB 뿐이라 값이 비용을 크게 넘는다.
        if (-not $hasArtifact -and $keptFailed -lt $KeepFailed -and (Test-Path (Join-Path $dir.FullName 'unity-build.log'))) {
            $keptFailed++
            continue
        }
        Write-Host "prune $($dir.FullName)$(if (-not $hasArtifact) { ' (산출물 없음)' })"
        Remove-Item $dir.FullName -Recurse -Force -ErrorAction SilentlyContinue
        $removed += $dir.Name
    }
    return $removed
}

# ── 이번 실행이 「무엇을 굽는가」 ───────────────────────────────────────────
# 사람이 손으로 돌릴 때는 입력이 채워져 오지만, 야간(schedule)·태그 실행에는 입력이
# 통째로 비어 온다. 그 빈칸을 각 단계가 알아서 메우면 단계마다 답이 갈린다 —
# 실제로 에디터 확인 단계는 플랫폼을 빈 문자열로 받아 **안드로이드인 줄 몰랐다**.
# 그러면 모듈이 없을 때 5초 만에 명확히 멈추는 대신, 빌드 한복판에서 알 수 없는
# 이유로 죽는다. 그래서 답은 여기서 *한 번만* 정하고 모든 단계가 그걸 읽는다.
function Get-EffectiveBuildConfig {
    param([string]$Platform, [string]$BuildType, [string]$EventName)
    if ([string]::IsNullOrWhiteSpace($Platform)) {
        # 야간 빌드는 폰에 설치해보려고 있는 것이라 안드로이드. 태그는 배포판이라 윈도우.
        $Platform = if ($EventName -eq 'schedule') { 'android' } else { 'windows' }
    }
    if ([string]::IsNullOrWhiteSpace($BuildType)) {
        $BuildType = if ($EventName -eq 'schedule') { 'development' } else { 'release' }
    }
    if ($Platform -notin @('windows', 'android')) { throw "알 수 없는 플랫폼: '$Platform'" }
    if ($BuildType -notin @('release', 'development')) { throw "알 수 없는 빌드 종류: '$BuildType'" }
    return @{ Platform = $Platform; BuildType = $BuildType }
}

function Get-LastBuiltMarker {
    param([string]$BuildRoot, [string]$Platform)
    return Join-Path $BuildRoot "last-built-$Platform.sha"
}

function Test-BuildNeeded {
    param([string]$BuildRoot, [string]$Platform, [string]$Sha)
    $marker = Get-LastBuiltMarker -BuildRoot $BuildRoot -Platform $Platform
    if (-not (Test-Path $marker)) { return $true }
    $last = (Get-Content $marker -Raw).Trim()
    if ($last -eq $Sha) {
        Write-Host "nightly skip: $Platform already built at $Sha"
        return $false
    }
    return $true
}

function Save-BuiltMarker {
    param([string]$BuildRoot, [string]$Platform, [string]$Sha)
    $marker = Get-LastBuiltMarker -BuildRoot $BuildRoot -Platform $Platform
    Set-Content -Path $marker -Value $Sha -Encoding ascii
}

# ── 「최신 빌드」 고정 카드 ─────────────────────────────────────────────────
# 개별 빌드 카드는 시간이 지나면 위로 밀리고 링크도 하루면 죽는다. 그래서 채널에
# *늘 같은 자리*(고정 메시지)에서 최신 산출물을 가리키는 카드 하나를 유지한다.
# 메시지 id 는 노트북에 남겨 계속 같은 메시지를 고쳐 쓴다 (새로 쌓지 않는다).

function Update-LatestCard {
    param([string]$Token, [string]$BuildRoot, [string]$Platform, [string]$OutDir,
        [string]$Report, [string]$Link, [string]$Commit, [string]$RunUrl,
        [string]$StatusWord, [string]$Icon)

    # 이 카드는 두 질문에 동시에 답한다:
    #   ① 지금 받을 수 있는 게 뭔가  → **마지막 성공** 빌드 (실패가 덮으면 어제 되던 것도 못 받는다)
    #   ② 마지막 시도는 어떻게 됐나  → 성공/실패/취소 무관하게 이번 결과
    # 그래서 성공 정보는 따로 저장해두고, 실패·취소 때는 그 저장본을 다시 쓴다.
    $successFile = Join-Path $BuildRoot "latest-success-$Platform.json"
    $success = $null

    if ($Link) {
        $version = ''
        $sizeText = ''
        if ($Report -and (Test-Path $Report)) {
            $json = Get-Content $Report -Raw | ConvertFrom-Json
            $version = $json.version
        }
        # 크기는 *받게 될 파일* 로 잰다. 리포트의 실행파일 크기를 쓰면 PC 빌드에서 거짓말을
        # 한다 — 실측: 카드엔 「0.6 MB」인데 실제 내려받는 묶음은 670 MB 였다. 폰은 산출물이
        # 파일 하나라 우연히 맞았을 뿐이다. 링크가 가리키는 이름을 그대로 읽어 잰다.
        if ($OutDir -and $Link -match '/dl/[^/]+/([^?]+)') {
            $linkedFile = Join-Path $OutDir ([Uri]::UnescapeDataString($Matches[1]))
            if (Test-Path $linkedFile) { $sizeText = "$([Math]::Round((Get-Item $linkedFile).Length / 1MB, 1)) MB" }
        }
        $success = [ordered]@{
            link = $Link; version = $version; size = $sizeText; commit = $Commit
            builtAt = (Get-Date).ToString('yyyy-MM-dd HH:mm')
        }
        $success | ConvertTo-Json -Compress | Set-Content -Path $successFile -Encoding utf8
    } elseif (Test-Path $successFile) {
        try { $success = Get-Content $successFile -Raw | ConvertFrom-Json } catch { $success = $null }
    }

    $attempt = "$Icon $StatusWord · $((Get-Date).ToString('MM-dd HH:mm')) KST"
    if ($success) {
        # 만료 안내는 링크 *자신* 에서 읽는다 (정본 = Get-LinkExpiryText).
        $expiryText = Get-LinkExpiryText -Link $success.link
        $expiryLine = if ($expiryText) { "이 링크는 **$expiryText KST** 까지 살아있다." } else { '' }
        $rich = @{
            # 폰은 눌러서 곧장 설치, PC 는 묶음을 받아 풀어야 한다 — 말이 다르면 안 된다.
            lead   = if ($Platform -eq 'android') { "## [📲 최신 android 빌드 설치]($($success.link))" }
                     else { "## [💻 최신 $Platform 빌드 받기 (zip)]($($success.link))" }
            title  = "⭐ 최신 $Platform 빌드"
            body   = "받을 수 있는 것은 **마지막으로 성공한 빌드**다. $expiryLine 만료됐으면 한 번 더 구우면 이 자리도 같이 바뀐다."
            fields = @(
                @{ name = '버전'; value = "v$($success.version)"; inline = $true },
                @{ name = '크기'; value = "$($success.size)"; inline = $true },
                @{ name = '커밋'; value = "$($success.commit)"; inline = $true },
                @{ name = '구운 시각'; value = "$($success.builtAt) KST"; inline = $true },
                @{ name = '마지막 시도'; value = $attempt; inline = $true }
            )
            level  = 'info'
            url    = $RunUrl
            footer = '이 카드 하나만 보면 현재 상태를 안다'
        }
    } else {
        # 아직 성공한 빌드가 없다 — 링크를 지어내지 않고 그 사실을 그대로 말한다.
        $rich = @{
            title  = "⭐ 최신 $Platform 빌드"
            body   = '아직 받을 수 있는 빌드가 없다. 한 번 성공하면 여기에 설치 링크가 생긴다.'
            fields = @(@{ name = '마지막 시도'; value = $attempt; inline = $false })
            level  = 'warning'
            url    = $RunUrl
            footer = '이 카드 하나만 보면 현재 상태를 안다'
        }
    }

    $stateFile = Join-Path $BuildRoot "latest-card-$Platform.id"
    $existing = $null
    if (Test-Path $stateFile) { $existing = (Get-Content $stateFile -Raw).Trim() }

    if ($existing) {
        if (Set-BuildCard -Token $Token -MessageId $existing -Rich $rich) { return }
        # 사람이 지웠을 수 있다 — 그러면 새로 만들고 id 를 갈아끼운다.
        Write-Host 'latest card missing, recreating'
    }
    $newId = New-BuildCard -Token $Token -Rich $rich
    if ($newId) {
        Set-Content -Path $stateFile -Value $newId -Encoding ascii
        Write-Host "latest card id=$newId (pin it once by hand)"
    }
}
