# WitchMendokusai — Claude 작업 지침

## 세계관

세계관은 `karmoddrine/memo/CLAUDE.md` 및 `karmoddrine/memo/wm/design/vision.md` 참고.

## 입력 처리

### 규칙

- **New Input System(`UnityEngine.InputSystem`)만 사용한다** — `Input.GetKeyDown`, `Input.GetAxis` 등 레거시 API 절대 금지
- **게임 컴포넌트에서 `Keyboard.current` / `Mouse.current` 직접 접근 금지**
  - 허용 예외: `InputManager` 내부, 카메라처럼 Update마다 연속값을 읽어야 하는 경우
- **모든 입력 이벤트는 `InputManager.RegisterInputEvent`를 통해 등록한다**

### 아키텍처

```
WMInput.inputactions          ← 바인딩 정의 (JSON 에셋)
    ↓
InputManager.BindEvents()     ← 앱 시작 시 모든 ActionMap 이벤트를 자동 구독
    ↓
InputManager.On{Start/Performed/Canceled}
    ↓
RegisterInputEvent()로 등록된 콜백들 호출
```

관련 파일:
- `Assets/_WitchMendokusai/Core/Scripts/Input/InputManager.cs`
- `Assets/_WitchMendokusai/Core/Scripts/Input/WMInput.inputactions`
- `Assets/_WitchMendokusai/Core/Scripts/Input/InputStrategy/` — 씬별 입력 전략

### 새 입력 이벤트 추가 절차

**0. 키 중복 확인 (필수, 먼저 수행)**
- `WMInput.inputactions`에서 `"path": "<Keyboard>/` grep으로 사용 중인 키 전체 목록 확인
- `InputManager.cs`의 `UpdateCameraRotateInput` / `UpdateMoveInput`에서 직접 읽는 키 확인 (Q, E, W, A, S, D, 방향키)
- 위 두 곳 모두 충돌 없는 키를 선택한 뒤 진행

1. **`InputManager.cs`** — `InputEventType` 열거형에 항목 추가
   ```csharp
   public enum InputEventType { ..., MyAction }
   ```

2. **`InputManager.cs`** — `inputEventBindings` 딕셔너리에 ActionMap 연결 추가
   ```csharp
   { InputEventType.MyAction, InputMapType.Player }
   ```

3. **`WMInput.inputactions`** — 해당 ActionMap에 Action 추가 (Unity 에디터 또는 JSON 직접 편집)
   - `name`: `InputEventType`과 동일한 문자열 (`"MyAction"`)
   - `type`: `"Button"` 또는 `"Value"`
   - `expectedControlType`: `"Button"`, `"Vector2"` 등
   - 바인딩 경로 예시: `"<Keyboard>/space"`, `"<Mouse>/scroll"`

4. **컴포넌트에서 등록/해제**
   ```csharp
   void Start() => InputManager.Instance.RegisterInputEvent(
       InputEventType.MyAction, InputEventResponseType.Performed, OnMyAction);

   void OnDestroy() => InputManager.Instance.UnregisterInputEvent(
       InputEventType.MyAction, InputEventResponseType.Performed, OnMyAction);

   void OnMyAction(InputAction.CallbackContext ctx) { ... }
   // 또는 Action 오버로드:
   void Start() => InputManager.Instance.RegisterInputEvent(
       InputEventType.MyAction, InputEventResponseType.Performed, () => DoSomething());
   ```

### `InputEventResponseType` 선택 기준

| 타입 | 발생 시점 | 적합한 용도 |
|------|----------|------------|
| `Started` | 키 누르는 순간 | 누르기 시작 감지 |
| `Performed` | 조건 충족 (Button: 누름 완료) | 단발 액션 (점프, 슬롯 선택 등) |
| `Canceled` | 키 떼는 순간 | 해제 감지 |
| `Get` | 누르고 있는 매 프레임 | 지속 입력 (이동 제외) |

> 이동(`Move`) 축은 `InputAxisType.Move` / `InputManager.MoveInput`으로 별도 처리된다.

### InputStrategyWorld 등록 방식

씬 전환 시 `InputManager`가 `SetInputStrategy(new InputStrategyWorld())`를 호출해 기존 이벤트를 초기화한다.  
`Start()`에서 직접 `RegisterInputEvent`하는 컴포넌트(예: `UIHotbar`)는 이 흐름과 독립적으로 동작하며, `OnDestroy()`에서 반드시 `UnregisterInputEvent`로 정리해야 한다.

## 코딩 스타일

### 타입 및 변수명
- `var` 사용 금지 — 항상 명시적 타입으로 선언한다
- 변수명 축약 금지 — `t`, `r`, `e` 같은 한 글자 축약 대신 `inputEventType`, `inputEventResponseType` 등 전체 이름 사용
  - 예외: `for` 루프의 `i`, `j` 같은 관용적 인덱스 변수
- 상수는 `UPPER_SNAKE_CASE` — `private const int NONE = -1`

### 불리언 비교
- 부정 조건은 `== false` 사용, `!` 연산자 사용 금지
  - `if (IsValidIndex(index) == false)` ✅ / `if (!IsValidIndex(index))` ❌

### 중괄호 및 포맷
- Allman 스타일 — 중괄호 항상 새 줄
- 단일 표현식 메서드/프로퍼티는 `=>` expression body 사용
  - `public void SetSlotIndex(int index) => Index = index;`

### 이벤트 및 델리게이트
- 이벤트/델리게이트 초기값은 `delegate { }` 또는 `= delegate { }` 로 null 방지

## 설계 컨펌

기존 코드 구조에 영향을 주는 변경(딕셔너리 분리/통합, 클래스 추가, 인터페이스 변경 등)은
구현 전에 반드시 설계 방향을 먼저 설명하고 사용자 확인을 받는다.
"이렇게 할게요" 가 아니라 "이렇게 하면 어떨까요?" 로 물어본 뒤 진행한다.

## 명령어 실행

git, grep, bash 등 명령어는 확인 없이 바로 실행한다.
되돌리기 어려운 작업(force push, 파일 삭제 등)은 예외.

## 자동화 우선 — 사용자에게 떠넘기지 말기

`.cs.meta` / `.asset` / `prefab` / Material / 씬 GameObject 등 **도구로 가능한 건 다 한 뒤** GUI/테스트만 사용자에게 요청한다.

- 새 BlockData/Building/Item .asset → 코드(`AssetDatabase.CreateAsset`)나 EditorWindow로 자동 생성. 사용자에게 "Inspector에서 만드세요" X
- 셰이더 만들었으면 그 Material .asset도 같이 자동 생성. SerializeField 박는 것까지 자동화 시도
- World 씬 GameObject 추가도 `manage_gameobject` 같은 도구로 자동
- 자동화 불가능한 것만(시각 검증, 물리 동작 등) 사용자에게 명시 요청

## TASK 단위 — 한 번에 한 단계

TASK 시드의 단계 분할 표(A1/A2/A3...)는 *합의된 작업 단위*다. 한 번에 한 단계만 진행한다.

- 사용자가 "A2 ㄱㄱ" → A2만. A3/A4까지 미리 가지 않는다.
- 한 단계 끝나면 *동작 검증* 또는 *컨펌* 받고 다음 단계
- 스코프 초과는 *검증 단위 깨짐* + *버그 위치 추적 어려움* + *사용자 페이스 무시*

예외: 사용자가 명시적으로 "다 묶어서" 또는 "전체 진행"이라고 한 경우.

## 새 시스템 도입 시 — 기존 패턴 확인

새 매니저/시스템을 만들 때 WM 기존 패턴(`Singleton<T>`, `OnXxxChanged` 이벤트, `InputManager.RegisterInputEvent`, `SOManager.DataSOs`, GameMode 구독 등)을 먼저 확인하고 정합성 맞춘다.

- 매니저 만들 때 → `Singleton<T>` 상속 검토 (BuildManager, AudioManager 등 선례)
- 입력 처리 → `InputManager.RegisterInputEvent` 사용 (Mouse/Keyboard 직접 접근 금지)
- 모드별 동작 → `GameModeManager.OnModeChanged` 구독 (Default/Build 모드 — TASK-025)
- 데이터 SO → `DataSO` 상속 + `SOManager` 등록 패턴 검토

새 시스템이 *우리 패턴과 다른 모양*이라면 *이유*를 TASK 시드에 명시한다.

## Editor 메뉴

`MenuItem` path 의 top-level root 는 **`WM/`** 단일화 (TASK-WM-057). `WitchMendokusai/...` 사용 X.

```csharp
[MenuItem("WM/<카테고리>/<항목>")]
```

카테고리 예: `WM/Setup/...` (부트스트랩), `WM/Voxel/...`, `WM/UGC/...`, `WM/Terrain/...`, `WM/ShaderModdingSDK/...` 등. grep 게이트: `MenuItem.*"WitchMendokusai/` 결과 0.

## 수치 노출 / 런타임 tweak

게임 시스템의 모든 *수치·시간·길이·가중치·확률* 은 하드코딩 금지. SO / `[SerializeField]` / `Variable<T>` 로 노출하고, **인스펙터에서 런타임 변경 → 즉시 반영** 되는 구조로 작성한다.

- 매니저는 SO 값 캐싱 X — 매 사용 시 다시 읽거나, 캐싱하면 변경 감지 후 갱신
- 자주 tweak 하는 값은 디버그 HUD 슬라이더 노출 (인스펙터 안 열어도 in-play tweak 가능해야)
- 같은 수치 두 곳에 박기 X — SO 가 정본, 매니저는 참조만
- 예외: 컴파일 타임 상수 (`const`) — frame 0, GameObject 키 문자열 등

이유: 런타임에 수치 못 바꾸면 디자인 iteration 비용 폭증. tweak 가능성은 *시스템 가치의 일부*. WM 의 "다른 게임 좋은 시스템 다 흡수" 비전과 직결.

## 에러 처리 — FastFail 유지

에러를 삼키는 방어 코드(TryGet, null 체크, 기본값 반환)로 증상을 덮지 말고,
FastFail을 유지한 채 등록 누락 등 근본 원인을 고친다.

방어 코드를 작성하기 전에 "왜 이 키/값이 없는가?"를 먼저 추적한다.
초기화·등록 경로가 누락된 것이면 그쪽을 고친다.
FastFail 메서드(직접 `[]` 접근 등)는 그대로 둔다.

## TASK 문서 갱신

TASK 기반으로 시작한 작업은 `memo/wm/tasks/TASK-NNN-*.md`를 작업 내내 갱신한다.

아래 시점마다 갱신:
- 설계 확정 시 → 핵심 결정사항, 데이터 흐름 채우기
- 파일 수정할 때마다 → 변경 파일 테이블 상태 업데이트 (⬜ → ✅)
- 방향이 바뀔 때 → 변경 이유와 새 방향 기록
- 작업 완료 시 → `status: done`, 에디터에서 남은 작업 명시

## 사용자 작업 기록

코드 작업 중 사용자가 직접 해야 하는 일(에디터 작업, 에셋 생성, 씬 배치, 테스트 등)이 생기면
해당 TASK 문서의 `## 에디터에서 남은 작업` 체크리스트에 즉시 기록한다.

대화 말미에 별도로 안내하는 것으로 끝내지 않고, 문서에 남겨 추적 가능하게 한다.

## 컴파일 에러 확인 — `dotnet build` 우선

코드 작성 후 사용자에게 "검증해주세요" 요청 *전*에 본인이 먼저 컴파일 검증.

- 컴파일 검증 = **`dotnet build`** (Unity foreground / stuck state 무관, 30~40초)
- Unity 가 필요한 건 *.meta / .prefab / .asset / 씬 / shader / Play Mode* 만 — `unity-refresh.ps1` 사용
- 사용자가 코드 보고 검증하기 전에 *컴파일 통과* 자체가 사전 조건
- "사용자에게 빨리 넘기기" 보다 *검증 가능한 상태로 넘기기* 가 우선

### 컴파일 검증 = `dotnet build` (Unity 무관)

```bash
cd WitchMendokusai
dotnet build Assembly-CSharp.csproj -v quiet --nologo 2>&1 | tail -20
```

- **Unity 가 stuck state 여도 작동** — Library/ScriptAssemblies / Editor.log 무관
- WM 의 `Assembly-CSharp.csproj` 는 Unity 가 자동 생성 — 모든 패키지 references 박아둠
- `오류 0개` / `error 0` 면 통과. warning 도 직접 출력 (deprecated API 등)
- 모든 `.cs` (멀티 worktree / 멀티 sub) 즉시 검증

### Unity 가 필요한 경우 = `unity-refresh.ps1`

자산 + Play Mode 시만:

```bash
pwsh memo/dotfiles/scripts/unity-refresh.ps1
# sleep ~25초 (Domain Reload + [InitializeOnLoadMethod])
```

용도:
- `.meta` 자동 생성 (새 .cs / 폴더)
- `.prefab` / `.asset` 자동 생성 (Bootstrap menu `[InitializeOnLoadMethod]`)
- 씬 / `RenderSettings` / Volume Profile / shader 컴파일
- Play Mode 진입 검증

검증: Editor.log grep — `Reloading assemblies after forced synchronous recompile` 새 매치 + Bootstrap 로그

### unity-refresh 한계 (이때만 사용자 손)

- Unity 창이 *이미 foreground* 면 `SetForegroundWindow` no-op (focus 이벤트 X) — 우회: PowerShell `SendKeys ^r` 또는 사용자가 다른 창 잠시 클릭
- Auto Refresh OFF → `Edit > Preferences > Asset Pipeline > Auto Refresh` 1회 컨펌
- 신규 폴더 파일은 fsnotify 가 가끔 누락 → Project 패널에서 폴더 한 번 클릭
- **Unity 컴파일 stuck state** (빈 .cs 가 일시 존재 후 컴파일 무응답) — `dotnet build` 로 코드 OK 검증 후 사용자에게 *Assets > Refresh* 메뉴 또는 Editor 재시작 1회 (unity-refresh / SendKeys 다 무효)

### 새 .cs 파일 만들었을 때

- `.cs.meta` 는 Unity 가 Editor focus 시 자동 생성 — 그 전엔 partial 또는 없음
- `[RequireComponent]` 자동 attach 도 Editor 가 prefab 인지해야 작동 — 사용자에게 *해당 prefab 한 번 열어 자동 보강 트리거* 요청

작업 완료 보고 흐름:
1. 코드 변경
2. **Editor.log 컴파일 에러 확인** + **stale 여부 확인** ← 둘 다 빠뜨리지 말 것
3. 에러 있으면 fix → 다시 1. stale 이면 사용자에게 reimport 요청 후 재확인
4. 통과 시 사용자에게 동작 검증 요청
5. 사용자 OK → commit

이전 사례: TASK-WM-034 B (NodeGraph GraphView UI) — `Func<GraphViewChange, GraphViewChange>` vs `GraphView.GraphViewChanged` delegate 타입 mismatch 컴파일 에러를 사용자가 먼저 발견. 본인이 Editor.log 안 보고 검증 요청해버림. 룰 추가 계기.

이전 사례 2: TASK-WM-039 Knockback A+B+C — `PlayerKnockbackCameraGlue` 신규 .cs 만든 후 Editor.log 봤는데 Unity foreground 안 와서 import 안 됨. 다른 (`HitstopFeedback`, `KnockbackFeedback`) 은 import 됐고 새로 만든 것만 안 됨 = stale 일 가능성 인지. 사용자에게 reimport 요청 → 통과 확인 후 검증 진행.

### Play Mode 진입 자동 lazy load — `RuntimeInitializeOnLoadMethod` 한계

새 매니저 prefab + `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` 로 Play Mode 시 자동 ensure 시:

- **컴파일 끝나기 *전* Play 진입하면 옛 .dll 사용** → 새 코드 적용 X. Play Mode 시작 후 *forced sync recompile* 발생 (Editor.log 의 `Reloading assemblies after forced synchronous recompile`) = 옛 .dll 시점에 RuntimeInitializeOnLoadMethod 이미 호출됨
- Unity well-known 한계: **assembly reload 시 RuntimeInitializeOnLoadMethod 재호출 X**

해결 흐름:
1. `unity-refresh.ps1` 호출 + sleep ~25초 + Editor.log grep (`error CS` 0 + `Reloading assemblies` 새 매치) 으로 *컴파일 완료 검증*
2. 검증 OK 면 사용자에게 Play 요청
3. Play 들어갔는데 새 코드 반영 안 되면 → Stop → 다시 Play (이번엔 새 .dll 로딩됨)

**`Singleton<T>` 매니저의 `dontDestroyOnLoad`** — prefab 의 `[SerializeField] dontDestroyOnLoad` 노브를 *true* 로 박는다. 씬 전환 시 destroy 되면 RuntimeInitializeOnLoadMethod(AfterSceneLoad) 1회만 호출이라 재인스턴스화 트리거 0. **코드로 `DontDestroyOnLoad()` 강제 호출은 X** — Singleton 베이스 SerializedField 가 정본 (수치 노출 / 런타임 tweak 룰 정합). 자동 부트스트랩 메뉴가 prefab 생성 시 SerializedField 박는 패턴.

이전 사례: TASK-WM-054-A WorldClock (2026-05-08) — Awake 에서 `DontDestroyOnLoad` 코드 강제 호출했다가 사용자가 "Singleton 베이스에 노브 있는데 왜 코드 강제냐" 지적. `WorldClockBootstrapMenu` 가 prefab 생성 시 SerializedField = true 박는 + idempotent `EnsurePrefabFlags` 패턴으로 정정.

## Git Workflow

본 § 가 본 레포 git workflow 의 정본. CodeRabbit 이 자동 픽업해 같은 룰로 PR 리뷰.

### 브랜치 + PR 강제 (AI Native 게이트)

`main` 직접 push 금지. 작업마다 브랜치 분기 후 PR.

- `feature/<주제>` — 새 기능
- `fix/<주제>` — 버그 fix
- `chore/<주제>` — 빌드·CI·의존성·환경 설정·문서
- `refactor/<주제>` — 동작 변화 없는 정리

**Default Ready PR 생성** (`gh pr create` 에 `--draft` *제거*) + `.github/pull_request_template.md` 의도 채움 → push → 자동 폐쇄 루프가 머지 + cascade 처리. AI Native 라 사용자 검토 슬롯 = AI 리뷰 (**Claude primary review** + CodeRabbit/Copilot secondary + Claude review-fix) 가 대체.

### AI Native 자동 폐쇄 루프 (2026-05-07 도입, 2026-05-08 Claude primary review 추가)

```
PR opened (Default Ready)
  ├─ claude.yml `claude-review` job (anthropics/claude-code-action@v1, mode=review)
  │    ↓ Claude primary review — 버그/보안/성능/품질 + WM 룰 체크 → review submit
  │      (approve / request_changes / comment)
  ├─ CodeRabbit / Copilot review (secondary informative — 무료 플랜 쿨타임 종속)
  │    ↓ review.submitted 이벤트 → claude.yml `claude-coderabbit` / `claude-copilot` job
  │      → Claude 가 actionable 제안만 fix-loop
  └─ auto-merge.yml (BOT_TOKEN, user actor)  ─── ready_for_review 이벤트 받아 gh pr merge --auto --squash
       ↓ Required 게이트 통과 (Code Quality typo strict + GitGuardian + claude-review + 옵셔널 Unity Build Gate)
       ↓ squash 머지 (user actor → push event 발생)
main push event
  ├─ auto-rebase.yml (BOT_TOKEN)  ─── 모든 open Ready PR update-branch
  │    ├─ 통과 PR: 자동 stale 해소
  │    └─ conflict PR: @claude 멘션 코멘트 (BOT_TOKEN)
  │       ↓
  │       claude.yml의 claude job (anthropics/claude-code-action@v1)
  │       ↓
  │       Claude 자동 conflict 해결 + push → 다시 게이트
  └─ Stack PR: GitHub 자동 retarget (base=feature → main) → 자동 머지 cascade
```

호출 (autopilot / 사람 PR 동일):
```bash
gh pr create --base main --head <branch> --title "..." --body "..."   # Default Ready (--draft X)
```

`auto-merge.yml` 이 자동으로 squash 머지 enable. 추가 호출 불필요.

### Draft 명시 사유 — *예외* 만 Draft

다음 명시적 사유 있을 때만 `--draft` 사용. PR description 에 "**Draft 사유**: <한 줄>" 명시:
- **Architectural / breaking change** — 사용자 비전 결정 슬롯 필요
- **stack PR base 가 미머지** — base 머지 후 GitHub 자동 retarget 까지 대기
- **큰 비주얼·런타임 신규 시스템** — sub-C C1 같은 *처음 도입* 비주얼/런타임 시스템 + 사용자 *플레이 흐름 자체* 검증 필요. 작은 변경 (SerializedField default toggle, ContextMenu, 5줄 fix) 은 Default Ready 머지 후 사용자 main pull 시점 검증으로 충분 — Unity Build Gate (TASK-WM-060) 가 컴파일 안전성 자동.
- **WIP 진행 중** — commit 누적, 완료 후 Ready 전환 (autopilot 자체는 매 iteration 단위 PR 분리라 흔치 X)

미명시 → 항상 Ready. *시각 검증* 자체는 PR 머지 후 사용자 흐름이 정합 — Draft 로 머지 지연 X.

### BOT_TOKEN — GitHub 재귀 방지 우회

`auto-merge.yml` / `auto-rebase.yml` 의 `GH_TOKEN` 은 `secrets.BOT_TOKEN` (PAT) 사용. **`secrets.GITHUB_TOKEN` X**.

이유: GitHub 의 well-known 한계 — `GITHUB_TOKEN` 으로 만든 commit / merge / comment 는 *재귀 방지* 룰로 다른 workflow 를 trigger 안 시킴.
- bot actor 머지 → main push event 가 auto-rebase trigger 안 함
- bot actor 코멘트 → claude.yml 의 claude job trigger 안 함

PAT (Personal Access Token, fine-grained, repo: Pull requests + Issues + Contents write) → user actor → 정상 trigger. 사용자 1회 발급 + `BOT_TOKEN` secret 등록.

자세한 진단: 2026-05-07 PR #102 (auto-rebase) / #103 (auto-merge) commit 메시지 참고.

### Closes #NN — Issue 자동 종료

PR description 에 관련 GitHub Issue 명시:
- TASK 시드에 Issue link 가 있거나 1:1 매핑이면 PR 본문 끝에 `Closes #NN`
- 머지 시 Issue 자동 close — wishlist 누적 방지
- 매핑 없으면 박지 X (스팸 X)

### Stack PR 자동 promote

base 가 다른 feature 브랜치인 stack PR 은 base 머지 시 GitHub 가 자동으로 main 으로 retarget. retarget 된 PR 도 Ready + auto-merge enabled 면 게이트 통과 후 자동 머지 → 연쇄. **stack 깊어도 base 만 풀리면 다 풀림**.

### Post-merge 정리

`delete_branch_on_merge: true` (repo 설정 — 원격 자동 삭제). 로컬 잔여만 정리:

```powershell
git fetch -p
git branch -vv | Select-String ': gone\]' | ForEach-Object { ($_ -split '\s+')[1] } | ForEach-Object { git branch -D $_ }
```

자율 모드는 자기 워크트리도 정리:

```bash
git worktree remove ../.worktrees/<name>
```

### 예외 — `main` 직접 push 허용

다음 경우만 PR 생략:
- 1~3줄 chore (오타 fix / 주석 갱신 / 단일 const 값 변경)
- README · CLAUDE.md 자체 minor 보강 (룰 한 줄 추가 등)

판단 기준: *코드 동작 변경 0* + *CodeRabbit 리뷰 가치 0*. 애매하면 PR 분기.

#### main 워크트리 더러울 때 — worktree 우회 패턴

다른 세션 dirty/untracked 가 main 워크트리에서 ff 를 막고 있을 때, "main 직접 push" 의 *근본 경로* 는 main 워크트리에서 commit 이 아니라:

```bash
git worktree add -b chore/<주제> ../.worktrees/<name> origin/main
# <name> 에서 편집 + commit
git push origin chore/<주제>:main      # 로컬 브랜치 → 원격 main 직접 푸시
git worktree remove ../.worktrees/<name>
git branch -D chore/<주제>
```

parallel 세션 dirty 안 건드리면서 chore 푸시. 1~3줄 chore 라도 워크트리 비용 정합 — 다른 세션 잔재 정리 시도 X (잔재 안전성은 `git hash-object <local>` vs `git rev-parse origin/main:<path>` 비교 후에만; 1개라도 diff 나면 잔재 정리 X, 즉시 worktree 우회).

이전 사례: 2026-05-08 ChunkMesher 로그 chore — 054-A 머지 후 WorldClock untracked 잔재 16개 중 `WorldClock.prefab` 1개에 살아있는 local 변경 발견. "잔재 정리 → ff" 가설 깨지고 worktree 우회로 전환 (커밋 `649023a8`).

### Commit 메시지

Conventional Commits — `feat: / fix: / chore: / refactor: / docs: / style: / test: / perf:`.
한 commit 한 주제. 메시지 = 실제 변경 일치.

### Branch Protection (사용자 GitHub 측 설정)

룰을 *기계적으로 강제* 하려면 GitHub repo → Settings → Branches 에서 `main` 에 다음 protection rule:
- Require a pull request before merging
- Require status checks to pass:
  - `Check Typos` (현재 등록됨, 단 `continue-on-error: true` — 사실상 게이트 0. workflow 에서 `continue-on-error` 제거 필요)
- Restrict who can push to matching branches (직접 push 차단)

설정 명령:

```bash
gh api -X PUT repos/Mascari4615/Witch-Mendokusai/branches/main/protection \
  -F 'required_status_checks.strict=true' \
  -F 'required_status_checks.contexts[]=Check Typos' \
  -F enforce_admins=false \
  -F required_pull_request_reviews=null \
  -F restrictions=null
```

### C# 컴파일 검증 — `Unity Build Gate` (self-hosted runner)

**(TASK-WM-060, 2026-05-08 도입)**

`.github/workflows/unity-build-gate.yml` 가 PR 마다 self-hosted runner 위에서 Unity batchmode 컴파일 검증. third-party action 0 (Unity credentials 외부 노출 X), 사용자 PC 의 Personal license 그대로.

```yaml
runs-on: [self-hosted, windows, wm-unity]
```

흐름:
1. PR push → workflow 트리거 (paths filter: `Assets/**/*.cs`, `Assets/**/*.shader`, `Packages/**`, csproj)
2. Self-hosted runner (사용자 PC) Unity batchmode 실행 — `-projectPath . -batchmode -nographics -quit -logFile -`
3. Unity exit code + log 의 `error CS` grep → check pass / fail
4. fail 시 `unity-batchmode-log` artifact 업로드 (사용자 진단)

**사용자 1회 셋업**:

1. GitHub repo *Settings > Actions > Runners > New self-hosted runner* 등록 — 라벨 `self-hosted, windows, wm-unity`
2. Branch Protection (`main`) 의 *Required status checks* 에 `Unity Build Gate / compile` 추가
3. runner 첫 실행 시 Library/PackageCache 캐시 회복 (Unity 한 번 batchmode 또는 Hub 에서 import)

**한계**:
- self-hosted runner = 사용자 PC. PC 꺼져있으면 PR check `queued`.
- 시각·런타임 검증 X — 컴파일 only. 시각은 사용자 main pull 후 Play Mode (현재 흐름 그대로).
- Unity 두 인스턴스 동시 실행 — 같은 PC 에서 가능 (다른 projectPath). main worktree Unity 와 runner Unity 별도 worktree 라 OK.

**현 게이트** (Unity Build Gate 추가 후): `Code Quality typo + GitGuardian + Unity Build Gate + Claude primary review + CodeRabbit/Copilot 리뷰 (secondary)` — auto-merge 자동 폐쇄 루프.

### AI 리뷰 게이트 — Claude primary review

**(TASK-WM-062, 2026-05-08 도입)**

`.github/workflows/claude.yml` 의 `claude-review` job 이 PR 마다 *primary code reviewer* 로 자동 review 코멘트 + submit.

```yaml
on:
  pull_request:
    types: [opened, synchronize, ready_for_review]

jobs:
  claude-review:
    if: github.event_name == 'pull_request' && github.event.pull_request.draft == false
    steps:
      - uses: anthropics/claude-code-action@v1
        with:
          mode: review
          claude_code_oauth_token: ${{ secrets.CLAUDE_CODE_OAUTH_TOKEN }}
          prompt: |
            (버그/보안/성능/품질 + WM CLAUDE.md 룰 준수 — Allman, == false, var 금지, 변수명 풀네임, FastFail, 수치 SO 노출, InputManager, MenuItem WM/)
            ...
```

**배경**: PR #119 (TASK-WM-058 P2-C) 가 push 후 26초 만에 머지됐는데 CodeRabbit 무료 플랜 45분 쿨타임으로 review 시작 X → AI 리뷰 0 으로 머지. 룰 본문 「AI 리뷰가 사용자 검토 슬롯 대체」 정합 깨짐.

**해결 흐름**:
- Claude = *primary reviewer* (쿨타임 X, OAuth 구독 비용 0)
- CodeRabbit / Copilot = *secondary informative* (review 도착 시 Claude 가 actionable fix-loop)
- `claude-review` job 의 status check 가 Branch Protection 의 Required 로 등록되면 머지 차단 게이트로 작동

**사용자 1회 셋업**:

1. `CLAUDE_CODE_OAUTH_TOKEN` secret — 이미 등록 (TASK-WM-062 sub-A 진단 결과)
2. Branch Protection (`main`) 의 *Required status checks* 에 `claude-review` 추가 (sub-B 머지 후 1번 작동 시 check name 등록 → 그 후 추가 가능)

**한계**:
- claude-review job 의 *job success* 만 status check 통과 신호 — review state (request_changes) 는 *관찰* 만, 머지 차단 X (GitHub 한계 — Required reviews 는 human reviewer 만 인정)
- 즉 Claude 가 *부정적 review* (request_changes) 를 남기더라도 Required check 통과는 됨. **review 코멘트** 가 머지 후 사용자에게 정합성 신호 역할.
