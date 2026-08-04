# WM 빌드 카드 — 디스코드 카드 하나를 만들고 그 자리에서 고쳐 쓴다 (TASK-WM-197).
#
# 왜: 빌드가 30~40분간 완전히 침묵한다. 그렇다고 단계마다 메시지를 쌓으면 채널이
# 진행 로그로 도배돼 정작 결과가 묻힌다. 그래서 카드 *하나* 를 계속 갱신한다.
#
# 누가 쓰나: build.yml 의 「Build player」(카드 생성 + 진행 갱신) 와
# 「Notify Discord」(같은 카드를 최종 결과로 마감). 두 step 이 같은 정의를 쓰도록
# 여기 한 곳에만 둔다.
#
# 인코딩: PS 5.1 은 BOM 없는 UTF-8 을 cp949 로 오독한다 → 이 파일은 BOM 유지 필수.

# StrictMode 는 걸지 않는다 — 이 파일은 *dot-source* 되므로 호출한 step 의 의미까지
# 바꿔버린다 (남의 코드 규칙을 조용히 갈아끼우는 셈).

$script:LaptopOpsUri = 'http://127.0.0.1:47615'

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
        $body = @{ channel = 'build'; rich = $Rich; wait = $true } | ConvertTo-Json -Depth 6 -Compress
        $response = Invoke-RestMethod -Uri "$script:LaptopOpsUri/notify" -Method Post `
            -Headers @{ Authorization = "Bearer $Token" } -ContentType 'application/json' -Body $body -TimeoutSec 20
        return $response.messageId
    } catch {
        Write-Warning "빌드 카드 게시 실패: $($_.Exception.Message)"
        return $null
    }
}

function Set-BuildCard {
    param([string]$Token, [string]$MessageId, [hashtable]$Rich)
    try {
        $body = @{ channel = 'build'; rich = $Rich; messageId = $MessageId } | ConvertTo-Json -Depth 6 -Compress
        Invoke-RestMethod -Uri "$script:LaptopOpsUri/notify" -Method Post `
            -Headers @{ Authorization = "Bearer $Token" } -ContentType 'application/json' -Body $body -TimeoutSec 20 | Out-Null
        return $true
    } catch {
        Write-Warning "빌드 카드 갱신 실패: $($_.Exception.Message)"
        return $false
    }
}

# 단계 사다리 — *실제 완료된 빌드 로그* 에서 뽑은 표식만 쓴다 (추측 금지).
# 순서는 로그 등장 순서 그대로. 윈도우 빌드는 안드로이드 전용 단계가 안 나타나므로
# 그 칸을 그냥 건너뛴다 (마지막으로 확인된 단계가 곧 현재 단계).
function Get-BuildStageLadder {
    return @(
        @{ Key = 'open';     Label = '프로젝트 여는 중';   Patterns = @('Refreshing native plugins', 'Initialize engine version') },
        @{ Key = 'compile';  Label = '스크립트 컴파일';    Patterns = @('DisplayProgressbar: Compiling Scripts') },
        @{ Key = 'prepare';  Label = '빌드 준비';          Patterns = @('Switch To Build Platform', 'Build Player Scripts', 'Processing Addressable Group') },
        @{ Key = 'bundle';   Label = '에셋 번들 굽기';     Patterns = @('Write Serialized Files', 'Archive And Compress Bundles', 'Post Processing Catalog Entries') },
        @{ Key = 'sdk';      Label = '안드로이드 도구 점검'; Patterns = @('Detecting Android SDK', 'Detect Android NDK', 'Check Android Player Settings') },
        @{ Key = 'native';   Label = '네이티브 변환 (IL2CPP)'; Patterns = @('Fetching assembly references') },
        @{ Key = 'package';  Label = '패키징 (Gradle)';    Patterns = @('Check gradle project collisions', 'Incremental Player Build', 'Building Gradle project') },
        @{ Key = 'finish';   Label = '마무리·서명';        Patterns = @('Build Successful', 'Validate Gradle Project', 'IPostGenerateGradleAndroidProject') }
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

# 진행 중 카드 본문. 경과 시간을 같이 보여준다 — 「몇 분째인가」가 「어느 단계인가」
# 만큼 궁금하고, 평소보다 오래 걸리는지도 이걸로 안다.
function New-ProgressRich {
    param(
        [int]$StageIndex,
        [string]$Platform,
        [string]$BuildType,
        [string]$Commit,
        [datetime]$StartedAt,
        [string]$RunUrl,
        [int]$RunNumber
    )
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
