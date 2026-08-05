# WM git hooks — TASK-WM-109-F

> 큰 변경 누적 시 회귀 commit 식별 비용 ↓. *post-commit advisory* 가 2차 안전망,
> **commit 규율(§ 커밋 규율)이 1차 안전망**. hook 은 차단 X — 정보만 흐른다.

## 동기

TASK-WM-109 이슈 6 — 한 세션 35+ commits 누적 → 사용자 Play 진입 → NRE 폭발
→ 어느 commit 이 원인인지 식별 비용 폭발 (5+ 회귀 사이클).

원인은 매 commit 후 *컴파일+부팅 검증 없이* 다음 commit 으로 진행한 것. CLAUDE.md
가 정한 검증 흐름 (`refresh_unity` → `read_console`) 을 매 commit 단위로 실행하지
않으면 `git bisect` 가 사실상 무력화된다 (모든 commit 이 NRE 면 갈라낼 게 없다).

## 무엇이 들어있나

| 파일 | 역할 |
|---|---|
| `post-commit` | bash POSIX hook. 매 commit 후 자동. PS verify 스크립트에 위임 (없으면 silent skip). 항상 exit 0 — *commit 차단 X*. |
| `wm-commit-verify.ps1` | 실제 검증 로직 (PowerShell). ledger append + `.cs` / `.meta` / `.asset` / `.prefab` / `.unity` 카운트 + `.cs` ↔ `.cs.meta` 짝 검사 + **은퇴한 식별자 canary** + Unity-MCP TCP probe(포트는 `.mcp.json` 에서 읽는다) + big-commit hint. *Unity 호출 X* (hook 은 빨라야). |
| `retired-identifiers.tsv` | 은퇴한 이름 표(`옛이름 <TAB> 새이름 <TAB> 사유`). 커밋의 *추가된 줄*에 옛 이름이 나오면 「옛 사본을 들고 있다」고 알린다 — 2026-08-06 하루에 네 번, 서로 다른 파일이 개명 전 내용으로 통째 되돌아와 main 이 `CS0246` 로 죽었다. **이름을 바꿀 때마다 한 줄 추가할 것**(로직 수정 불요). 막지는 않는다. |
| `install.ps1` | `.git/hooks/post-commit` 으로 hook 복사. git common dir 사용 → 메인 + 모든 worktree 한 번에 적용. idempotent (이미 설치돼 있으면 no-op). `-Force` / `-Uninstall` 지원. |

## 설치

```powershell
powershell -File Tools/git-hooks/install.ps1
```

- 재설치: `-Force`
- 제거: `-Uninstall`

옵트인 — 자동 활성화 X. 팀에서 적용 합의되면 각 환경에서 한 번씩 실행. (`.git/hooks/`
는 git 추적 외 — 레포 clone 직후엔 비어있어, 매 머신·worktree 셋업의 일부.)

## 산출 — `<git-common-dir>/wm-commit-log.tsv`

매 commit 마다 한 행 append. 컬럼:

```
ts  sha  author  cs  meta  asset  prefab  scene  mcp  parents  subject
```

- `ts` — ISO-8601 (timezone 포함)
- `sha` — short (12자)
- `cs`, `meta`, `asset`, `prefab`, `scene` — commit 내 touched 파일 수
- `mcp` — 0/1, *commit 시점* Unity-MCP `:8080` TCP probe 응답 여부
- `parents` — 부모 commit 수 (2+ = merge, diff stat 미계산)
- `subject` — commit subject line (tab → space 정리)

활용:

```powershell
# 최근 30개 commit 한 눈에
Get-Content (git rev-parse --git-common-dir | %{ Join-Path $_ wm-commit-log.tsv }) -Tail 30

# big commit 만
Import-Csv -Delimiter "`t" -Path (...) | Where-Object { [int]$_.cs -gt 10 }

# MCP 응답 없었던 commit (검증 누락 의심 구간)
Import-Csv -Delimiter "`t" -Path (...) | Where-Object { $_.mcp -eq '0' }
```

`mcp=0` 구간이 NRE 후보 zone — 그 시점 Editor 가 응답을 안 했으므로 컴파일 검증이
누락됐을 가능성 ↑. bisect 시 좁히는 단서.

## 커밋 규율 (1차 안전망)

post-commit hook 은 *정보만* — 차단 X. **근본은 commit 규율** (TASK-WM-109-F 의
핵심 결론). CLAUDE.md § Git Workflow 의 정본 룰을 본 TASK 의 *bisect 친화성* 관점
으로 재확인:

### 5가지 원칙

1. **1 commit = 1 logical change** — bisect 가능해야. `.cs` 30개 한 commit 은 NRE
   추적 비용 폭발 (본 TASK 의 동기 그 자체).
2. **각 commit 은 *독립적으로* 빌드 + 부팅** — 중간 commit 이 compile fail 이면
   `git bisect` 가 그 구간을 못 가른다. WIP 면 squash 먼저, push 는 그 후.
3. **`.cs` 와 `.cs.meta` 는 같은 commit** — Unity 가 `.meta` 못 보면 GUID 재발급 →
   씬 ref 깨짐. CLAUDE.md "Unity 자연 단위 commit" 정합.
4. **dependent asset 동행** — `.cs` 가 참조하는 SO / `.asset` / `.prefab` / 씬
   `.unity` 분리 금지. pull race 위험 (다른 worktree 가 절반만 받음).
5. **Conventional Commits** — `feat:` `fix:` `chore:` `refactor:` `docs:` `style:`.
   PR 폐기로 단위 자유도 ↑ 이므로 *더 잘게* 잘라도 OK (그 게 bisect 친화).

### bisect 흐름 (회귀 의심 시)

```powershell
# 1) ledger 에서 마지막 known-good SHA 찾기
$ledger = Join-Path (git rev-parse --git-common-dir) 'wm-commit-log.tsv'
Get-Content $ledger -Tail 30

# 2) bisect 시작
git bisect start
git bisect bad HEAD              # 현재 = NRE 발생
git bisect good <known-good-sha> # ledger 에서 본 SHA

# 3) 각 step Unity Editor 에서 Play 검증, 결과 표시
#    (각 commit 이 § 원칙 2 를 지켰다면 모든 중간 step 이 부팅 가능)
git bisect good   # 또는 bad
# ... 반복 ...
git bisect reset
```

ledger 의 `cs` / `mcp` 컬럼이 bisect 시작 범위를 좁히는 단서:

- `mcp=1` 연속 구간 = 그 시점 Editor 응답 OK = 검증 가능했던 구간
- `cs=0` commit = .cs 변경 0 = NRE 발생 가능성 ↓ (스킵 후보)

## Unity-MCP 와의 관계

CLAUDE.md § Unity-MCP layer 의 `read_console` 이 컴파일 검증 정본 (Mono runtime
직속). hook 자체는 MCP 를 *호출* 하지 않는다:

- JSON-RPC handshake 가 1회성 PS 호출에 무겁고, hook 은 빨라야 한다 (수 ms).
- MCP 호출 결과로 commit 을 *차단* 할 수도 없다 (post-commit). 정보 가치만 있는데
  비용이 크다.

대신 *MCP 응답 여부만* TCP probe (~300ms timeout) 로 ledger 에 기록 — 추후 분석
시 "그 시점 Editor 가 살아있었는지" 단서.

진짜 컴파일 검증은:

- 자동: 에이전트가 `refresh_unity(...)` + `read_console(types=["error","warning"])`
  실행 (CLAUDE.md 정본).
- fallback: Editor.log grep (append-only 한계 — § 컴파일 에러 확인 참고).
- 부팅 검증: `wm-boot-smoke.ps1` (memo dotfiles, batchmode standalone superset).

hook 의 책임은 *유도* + *기록* 뿐, *증명* 이 아니다.

## 비활성화 / 우회

- 영구 비활성화: `powershell -File Tools/git-hooks/install.ps1 -Uninstall`
- 임시: post-commit hook 은 `git commit --no-verify` 영향을 받지 *않는다* (그 옵션은
  pre-commit / commit-msg 만). 정말 우회하려면 그 1 commit 동안 hook 파일을 .bak
  으로 옮기거나 `core.hooksPath` 를 비표준 경로로 일시 변경.
- 단일 commit 영향 없음 — hook 은 *post* 단계, 이미 commit 된 뒤 실행. 실패해도
  rollback 안 일어남.

## 트러블슈팅

| 증상 | 원인 / 대처 |
|---|---|
| commit 직후 console 출력 없음 | hook 미설치 — `install.ps1` 재실행. `.git/hooks/post-commit` 존재 확인. |
| `no pwsh/powershell on PATH` | PowerShell 미설치. Windows 면 정상 X. Git Bash 환경변수에서 `powershell` 찾을 수 있는지 확인. |
| `wm-verify` 라인 보이는데 ledger 안 생김 | `.git/wm-commit-log.tsv` 권한 / 경로 — 메시지의 ledger 경로 확인. |
| `mcp=0` 으로 계속 찍힘 | Unity Editor 미실행 / `Window > MCP for Unity > Start Server` 미시작. CLAUDE.md § Unity-MCP layer 참고. |
| 대량 .cs commit 후 [big] 경고 | 의도된 경고 — § 커밋 규율 5원칙 #1 위반 가능 신호. 다음 commit 부터 분할 검토. |

## TASK-WM-109-F 적용 외 사용처

이 ledger 는 회귀 추적 외에도:

- **자가증강 baseline 측정** (agent-mission §2.8-C) — 어떤 시기에 commit 빈도 / .cs
  density / MCP 가용성이 어땠는지 객관 기록. retro 가 진화 적합도 평가에 사용 가능.
- **세션 후처리** — Claude 세션 끝나고 "이 세션 commit 들 .cs 총량 / big commit 수"
  를 ledger 한 번 grep 으로 확인.
- **자동 단위 분할 가이드** — `cs > N` commit 자동 detect → 다음 작업에서 같은
  영역 commit 단위 조정 reminder.
