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

# 카드 게시 → 메시지 id 반환 (이후 갱신의 손잡이). 실패해도 $null 만 돌려준다:
# 알림이 빌드 성패를 뒤집으면 안 된다.
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
# 순서는 로그 등장 순서 그대로. 윈도우 빌드는 안드로이드 전용 단계가 안 나타나므로
# 그 칸을 그냥 건너뛴다 (마지막으로 확인된 단계가 곧 현재 단계).
function Get-BuildStageLadder {
    return @(
        @{ Key = 'open';    Label = '프로젝트 여는 중';      Patterns = @('Refreshing native plugins', 'Initialize engine version') },
        @{ Key = 'compile'; Label = '스크립트 컴파일';       Patterns = @('DisplayProgressbar: Compiling Scripts') },
        @{ Key = 'prepare'; Label = '빌드 준비';             Patterns = @('Switch To Build Platform', 'Build Player Scripts', 'Processing Addressable Group') },
        @{ Key = 'bundle';  Label = '에셋 번들 굽기';        Patterns = @('Write Serialized Files', 'Archive And Compress Bundles', 'Post Processing Catalog Entries') },
        @{ Key = 'sdk';     Label = '안드로이드 도구 점검';  Patterns = @('Detecting Android SDK', 'Detect Android NDK', 'Check Android Player Settings') },
        @{ Key = 'native';  Label = '네이티브 변환 (IL2CPP)'; Patterns = @('Fetching assembly references') },
        @{ Key = 'package'; Label = '패키징 (Gradle)';       Patterns = @('Check gradle project collisions', 'Incremental Player Build', 'Building Gradle project') },
        @{ Key = 'finish';  Label = '마무리·서명';           Patterns = @('Build Successful', 'Validate Gradle Project', 'IPostGenerateGradleAndroidProject') }
    )
}

# 새로 붙은 로그 조각에서 가장 앞선 단계를 찾는다. 되돌아가지 않는다(단조 증가) —
# 로그에는 옛 단어가 다시 나올 수 있는데 진행 막대가 뒤로 가면 그게 더 헷갈린다.
function Get-StageIndexFromChunk {
    param([string]$Chunk, [int]$CurrentIndex)
    $ladder = Get-BuildStageLadder
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
        [datetime]$StartedAt, [string]$RunUrl, [string]$RunNumber)
    $ladder = Get-BuildStageLadder
    $stage = $ladder[$StageIndex]
    $elapsed = [Math]::Round(((Get-Date) - $StartedAt).TotalMinutes, 1)
    return @{
        title  = "⏳ WM 빌드 $Platform — 진행 중"
        body   = "$(Get-ProgressBar -Index $StageIndex -Total $ladder.Count)  $($StageIndex + 1)/$($ladder.Count) · **$($stage.Label)**"
        fields = @(
            @{ name = '플랫폼'; value = "$Platform / $BuildType"; inline = $true },
            @{ name = '경과';   value = "$elapsed 분"; inline = $true },
            @{ name = '커밋';   value = $Commit; inline = $true }
        )
        level  = 'progress'
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
    }

    Write-Host "Unity args: $($arguments -join ' ')"
    $process = Start-Process -FilePath $UnityExe -ArgumentList $arguments -PassThru -WindowStyle Hidden

    $reader = $null
    $lastPost = Get-Date
    $lastStage = -1
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
                if ($chunk) { $stageIndex = Get-StageIndexFromChunk -Chunk $chunk -CurrentIndex $stageIndex }
            }
        } catch {
            Write-Warning "log poll failed (ignored): $($_.Exception.Message)"
        }
        # 단계가 바뀌었거나 5분이 지났으면 갱신 (Discord rate limit 여유).
        if ($cardId -and (($stageIndex -ne $lastStage) -or (((Get-Date) - $lastPost).TotalMinutes -ge 5))) {
            $lastStage = $stageIndex
            $lastPost = Get-Date
            Set-BuildCard -Token $token -MessageId $cardId -Rich (New-ProgressRich -StageIndex $stageIndex -Platform $Platform `
                    -BuildType $BuildType -Commit $Commit -StartedAt $startedAt -RunUrl $RunUrl -RunNumber $RunNumber) | Out-Null
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
function Get-InstallLink {
    param([string]$Token, [string]$OutDir)
    if (-not $OutDir -or -not (Test-Path $OutDir)) { return $null }
    $artifact = Get-ChildItem $OutDir -File | Where-Object { $_.Extension -in '.apk', '.aab' } | Select-Object -First 1
    if (-not $artifact) { return $null }
    try {
        # 지금은 경로가 ASCII 뿐이지만 같은 창구를 쓴다 — 예외를 두면 언젠가 그 예외로 샌다.
        $signed = Invoke-LaptopOpsJson -Path '/dl/sign' -Token $Token `
            -Payload @{ build = (Split-Path $OutDir -Leaf); file = $artifact.Name; days = 1 }
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
        $lead = "## [📲 폰에 설치하기]($link)"
        $bodyText = '링크 유효 1일 · 처음 한 번만 비밀번호 (고정 메시지 참고)'
    } elseif ($ok) {
        $bodyText = '산출물은 노트북에만 있다 (단일 파일이 아니라 설치 링크 없음).'
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

    # 진행 카드가 있으면 *그 자리에서* 결과로 마감한다 — 진행하던 자리에 결과가 남는 게
    # 자연스럽고, 새 메시지를 또 쌓지 않는다. 못 고치면 그때만 새로 게시한다.
    if ($CardId) {
        $updated = Set-BuildCard -Token $token -MessageId $CardId -Rich $rich
        Write-Host "card closed ok=$updated id=$CardId link=$([bool]$link)"
        if (-not $updated) { New-BuildCard -Token $token -Rich $rich | Out-Null }
    } else {
        $posted = New-BuildCard -Token $token -Rich $rich
        Write-Host "card posted id=$posted link=$([bool]$link)"
    }

    # 성공한 빌드만 「최신」 자리를 갱신한다. 실패가 최신을 덮으면 어제 되던 것마저
    # 못 받게 된다.
    if ($ok -and $link) {
        Update-LatestCard -Token $token -BuildRoot $BuildRoot -Platform $Platform -OutDir $OutDir `
            -Report $Report -Link $link -Commit $Commit -RunUrl $RunUrl
    }
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
        [string]$Report, [string]$Link, [string]$Commit, [string]$RunUrl)
    if (-not $Link) { return }   # 설치 링크가 없는 산출물은 「최신」으로 걸 이유가 없다

    $version = ''
    $sizeText = ''
    if ($Report -and (Test-Path $Report)) {
        $json = Get-Content $Report -Raw | ConvertFrom-Json
        $version = $json.version
        $mb = [Math]::Round($json.exeSizeBytes / 1MB, 1)
        $sizeText = "$mb MB"
    }
    $rich = @{
        lead   = "## [📲 최신 $Platform 빌드 설치]($Link)"
        title  = "⭐ 최신 $Platform 빌드"
        body   = '이 카드는 빌드가 끝날 때마다 최신 것으로 갱신된다. 링크는 하루 뒤 만료되니, 만료됐으면 새로 빌드하면 이 자리도 같이 바뀐다.'
        fields = @(
            @{ name = '버전'; value = "v$version"; inline = $true },
            @{ name = '크기'; value = $sizeText; inline = $true },
            @{ name = '커밋'; value = $Commit; inline = $true },
            @{ name = '구운 시각'; value = (Get-Date).ToString('yyyy-MM-dd HH:mm') + ' KST'; inline = $false }
        )
        level  = 'info'
        url    = $RunUrl
        footer = '고정해두면 언제든 여기서 최신 것을 받는다'
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
