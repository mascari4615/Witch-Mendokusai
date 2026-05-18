---
task_id: TASK-WM-109
session: "2026-05-14 ~ 2026-05-15"
commits: "35+"
type: retrospective
status: issue-tracking
---

# TASK-WM-109 — 세션 회고 (2026-05-14 ~ 2026-05-15)

**목표**: 본 세션에서 어려웠던/복잡했던/인프라 부족 항목을 issue로 트래킹. **해결 방법은 비워둔다** (후속 세션이 진단+해결).

## 이슈 1 — VContainer 메커니즘 학습 늦음 → 가설 박기 4회 반복

### 문제
VContainer의 정확한 동작(source generator / RegisterComponent* / Inject vs InjectGameObject / scope hierarchy)을 *처음부터 source 정독* 하지 않고 *가설 기반 fix* 반복.

- "abstract base [Inject] field 처리 한계" — 가설, 틀림
- "Init Awake → Start" race — 가설, 무관
- "field 인젝션" — 가설, 무관  
- "SetBaseDeps 패턴" — 가설, 불필요였을 수 있음

VContainer source 정독(Agent A) 후에야 *진짜 동작 모델* 확보. 이미 5+ commits 가설 박은 후.

### 본 세션 증거
- commit: a7e04f35 ~ ad08b6eb 일대 가설 fix 다수
- 시간 소비: NRE 4회 반복(각 회귀 fix + 재컴파일 + Play 검증)
- 사용자 지적(2026-05-15): "이런 우회들을 해야하는 이유가 뭔지는 근본적으로 뭔지 확실하게 알고 수정하는건가요? 그냥 임시 처리만 하고 있는건가요?"

### 근본 원인
- CLAUDE.md의 "황금의 정신 — 가설 박기 X" 룰 위반
- 정독 대신 가설 → fix loop 반복

### 해결 방법
TBD — 후속 세션.

---

## 이슈 2 — VContainer InjectGameObject 자기 cascade → Unity crash

### 문제
Player.Construct에 `container.InjectGameObject(gameObject)` 도입 → 자기 자신 Player 컴포넌트도 cascade → Player.Construct 재호출 → 무한 재귀 → stack overflow → Unity crash.

CLAUDE.md의 "황금의 정신 — 설계 자가 검토" 룰 *위반*. 「이거 그냥 진행」 식 가설 박음. self-cycle 검증 X.

### 본 세션 증거
- commit: ad920ca2(도입) → bcb59577(revert)
- Unity가 Play 시 즉시 crash
- 사용자 발화(2026-05-15): "엥 유니티가 꺼져버림 Play 누르니까"

### 근본 원인
- 설계 단계에서 self-exclude guard 검토 누락
- VContainer InjectGameObject의 cascade 범위 미이해

### 해결 방법
TBD — 후속 세션. (VContainer InjectGameObject 사용 시 self-exclude guard 표준화 또는 caller 컴포넌트가 자기 자신 호출 안 하도록 정합 또는 별도 helper).

---

## 이슈 3 — 씬 직접 배치 컴포넌트 매번 grep 발견

### 문제
World.unity에 직접 배치된 컴포넌트(Dummy/MineralBase/InteractiveMarker/AutoAimMarker)가 NRE 날 때마다 grep으로 *씬 배치 여부* 추적. 자동화 부재.

각 NRE마다:
1. stack trace 확인
2. .meta GUID 검색
3. .unity / .prefab grep
4. 직접 배치 확인 후 SceneLifetimeScope FindObjectsByType cascade 추가

확장성 0 — 새 컴포넌트 추가 시 같은 사이클 반복.

### 본 세션 증거
- 4 commits 비슷한 패턴(e6d45a19 / ac9b1d12)
- "스캔 안 한 inject 미등록 컴포넌트가 어디 있나?" 매번 NRE 후에야 발견

### 근본 원인
- 씬 배치 컴포넌트의 자동 등록 메커니즘 부재
- audit 도구 미지원

### 해결 방법
TBD — 후속 세션. (audit 스크립트 / [Inject] marker interface / `autoInjectGameObjects` Inspector 통합 / RegisterAllInScene 자동화 등 방향성 검토 영역).

---

## 이슈 4 — 컴파일 검증 자동화 부재

### 문제
본 프로젝트 Unity Editor의 Unity-MCP 서버 등록 안 됨(다른 worktree의 Editor는 등록됐을 가능성). `read_console` 직접 호출 불가.

Fallback = Editor.log grep:
- *append-only* 누적 — 옛 컴파일 결과 섞임(부정확)
- `Reloading assemblies` 마커로 last reload 식별 → 그 후 grep
- `unity-refresh.ps1`이 Unity 못 찾으면 reload 트리거 0 — 사용자 GUI 클릭 필요
- Auto Refresh가 OFF면 코드 변경 후 자동 컴파일 X

본 세션 매 reload 요청 사이클 = 사용자 손(Unity 클릭) 필요.

### 본 세션 증거
- "Unity Editor for WitchMendokusai not running" 메시지 N회
- hook의 *cached Editor.log*가 옛 결과 보고 = false positive/negative

### 근본 원인
- worktree별 Unity Editor 인스턴스 관리 미정합
- MCP 자동 라우팅 부재

### 해결 방법
TBD — 후속 세션.

---

## 이슈 5 — 분산된 cascade trigger 11+

### 문제
VContainer DI 마이그 도중 *cascade Inject*가 11+ 곳에 분산:
- SceneLifetimeScope build callback(eager Resolve + FindObjectsByType 루프)
- ObjectPoolManager.CreateObject(InjectGameObject)
- UIManager.Awake(Instantiate cascade + AddComponent cascade)
- UIRoot.Start(CreateViews + 명시 Inject)
- UIDataGrid.Init / UISlot.Init / UIPanelGroup.Start(부모 cascade)
- Player/MonsterObject/ResourceNodeObject.Construct(self cascade)

새 컴포넌트 type 추가 시 *어디 cascade 박을지 결정 필요* — 확장성 0.

### 본 세션 증거
- audit Agent B(2026-05-15)가 식별: "8가지 cascade trigger"
- 본 세션이 추가한 cascade 후 *patching 누적* 진단

### 근본 원인
- DI graph의 중앙집중식 정의 부재
- cascade point 분산

### 해결 방법
TBD — TASK-WM-108 Phase A-F가 cleanup 계획. 본 issue는 *문제 명시*.

---

## 이슈 6 — 큰 변경 누적 시 회귀 디버깅 비용 폭발

### 문제
본 세션 35+ commits 누적 + 매 commit 후 Play 검증 안 함(Unity-MCP 미등록 + Auto Refresh 미작동 race). 사용자가 *한꺼번에 Play* 시 NRE 폭발.

각 NRE 추적이 *어떤 commit의 변경이 야기했나* 식별 어려움. 본 세션이 도입한 가설 fix가 *다른 NRE의 원인이 되는* race 다수.

### 본 세션 증거
- NRE 회귀 5+ 사이클(각 사이클 = 1-2 commits + Play + 진단 + commit)
- 사용자 발화: "이런 우회들 근본 X" + "황금의 정신 잊지 말기"

### 근본 원인
- 작은 단위 commit + 매 commit 검증 프로세스 미준수
- 통합 회귀 검증 자동화 부재

### 해결 방법
TBD — 후속 세션. (작은 단위 commit + 매 commit 검증 / 진단 도구 자동화 / 회귀 검증 hook 등).

---

## 이슈 7 — Unity-MCP 등록 안 된 워크트리에서 Editor 상태 추적 한계

### 문제
본 프로젝트(WitchMendokusai) Unity Editor가 *별도 worktree에서 띄워졌을 가능성*. unity-refresh.ps1이 "Unity Editor for WitchMendokusai not running" 보고 — 다른 worktree의 Editor 인스턴스라 매칭 안 됨.

세션 도중 Editor가 *crash + 재시작* 했음(Play 시 InjectGameObject cycle).

multi-worktree + multi-Editor 환경에서 *현재 작업 worktree의 Editor* 식별 + MCP 라우팅 자동화 부재.

### 본 세션 증거
- unity-refresh.ps1 "not running" N회
- Unity crash 후 재시작 + reload count = 1 fresh

### 근본 원인
- worktree-aware Unity 감지 로직 부재
- MCP multi-instance 라우팅 미정합

### 해결 방법
TBD — 후속 세션. (worktree-aware Unity 감지 / MCP multi-instance 라우팅 정합).

---

## cross-cut 관련 TASK / 룰

- `memo/rules/process.md § 황금의 정신 — 가설 박기 X` (본 세션 룰 추가)
- `memo/rules/process.md § 황금의 정신 — 설계 자가 검토` (본 세션 룰 추가)
- `memo/rules/task.md § 세션 종료 시 컨텍스트 핸드오프` (본 세션 룰 추가)
- `memo/rules/task.md § 세션 종료 시 이슈 트래킹` (본 세션 룰 추가)
- `memo/wm/tasks/TASK-WM-108-service-locator-안티패턴-제거-후속.md` (Phase A-F cleanup)
- `WitchMendokusai/CLAUDE.md § Unity-MCP layer` (MCP 셋업 정본)

---

## 요약

본 세션(2026-05-14 ~ 2026-05-15)의 35+ commits는 VContainer DI 마이그레이션 진행 중 다음 7가지 인프라 문제/패턴을 노출:

1. **근본 학습 누락** (가설 기반 fix 반복)
2. **설계 자가검토 부재** (self-cycle crash)
3. **씬 컴포넌트 자동화 부재** (grep loop)
4. **컴파일 검증 자동화 부재** (Editor.log fallback)
5. **DI cascade 분산** (11+ trigger points)
6. **회귀 검증 프로세스 미준수** (big bang integration)
7. **워크트리 + MCP 멀티 인스턴스 미정합**

모든 이슈는 **후속 세션이 진단 + 해결안 검토**하도록 deferred. TASK-WM-108 Phase A-F와 연계.
