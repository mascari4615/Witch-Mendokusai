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
powershell -File memo/dotfiles/scripts/unity-refresh.ps1
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

## Unity-MCP layer (TASK-WM-071, 2026-05-09)

**Unity 공식 MCP server 도입** (`com.unity.ai.assistant 2.7+`) — Claude 가 Unity Editor 직접 조작 가능. 기존 `dotnet build` + `unity-refresh.ps1` + Editor.log grep 흐름을 **보완·대체**.

### 사용 우선순위 — read 자율 / write 신중

**Read 도구 (자율 사용 OK)** — 읽기만, 부작용 0:
- `read_console` — Editor.log grep 대체. 실시간 + 구조화 (types, count, format)
- `mcpforunity://editor/state` — `is_compiling` / `ready_for_tools` / `is_domain_reload_pending` 직접 polling. Editor.log grep 보다 정확
- `find_gameobjects` — 씬 정합성 검증 (사용자에게 "Hierarchy 봐달라" 요청 X)
- `manage_camera(action="screenshot", include_image=True)` — 시각 검증 자동 (사용자에게 "어떻게 보여요?" X)
- `run_tests` — EditMode / PlayMode 자동
- `unity_reflect` / `unity_docs` — API 정확도 (추측 박지 X)
- `mcpforunity://scene/...` 리소스 시리즈 — hierarchy / volumes / cameras 등

**Write 도구 (사용자 컨펌)** — destructive 가능, 영향 면적 ↑:
- `create_script` / `script_apply_edits` — `.cs` 신설/수정 (자동 reimport + 컴파일)
- `manage_gameobject(action="create"/"modify"/"delete")` — 씬 GameObject 변경
- `manage_components.set_property` — Inspector 값 변경 (★ **수치 노출 룰 위반 위험** — SO 정본 우회 가능. 디버그 외에는 SO 통해 변경)
- `manage_assets` / `manage_prefabs` — 에셋 / prefab 변경
- `manage_packages` — 패키지 변경

### 기존 흐름과 결합

| 작업 | 기존 | + MCP layer |
|---|---|---|
| 컴파일 검증 | `dotnet build Assembly-CSharp.csproj` (Unity 무관) | + `read_console(types=["error"])` 보완 |
| Reimport 트리거 | `unity-refresh.ps1` (focus + sleep 25초) | `create_script` / `script_apply_edits` 자동 처리 (refresh_unity 불필요). polling = `mcpforunity://editor/state.is_compiling` |
| 씬 정합성 | 사용자 Hierarchy 시각 | `find_gameobjects` + `mcpforunity://scene/gameobject/{id}` |
| Play Mode 검증 | 사용자 Play 클릭 + Console 봐달라 | `run_tests` (EditMode/PlayMode) 자동 + `read_console` |
| 시각 검증 (회귀) | 사용자 "이상해 보여요" | `manage_camera(action="screenshot", include_image=True)` 자동 비교 |

### Multi-instance + worktree 정합

- `mcpforunity://instances` 로 활성 Unity Editor 목록 확인
- `set_active_instance(instance="...")` 으로 *현재 작업 worktree* 의 Unity 인스턴스 선택
- Multi-worktree 환경 (TASK-WM-069 인프라) 정합 — 각 worktree Editor 별 MCP routing

### 사용자 손 보존 영역 (MCP 도입 후에도)

**MDD 정합 — Yon (개발자) 이 게임을 *놀이처럼* 만지는 비전 보존**:
- **비전 결정** — "어색한 거 있어?", "이 디자인 OK?", "다음에 뭐 만들지?"
- **외부 GUI** — Unity Hub Add, GitHub UI, Slack 등
- **특수 시각 검증** — Custom Inspector / Editor Window / Animation 미세 조정 (MCP 가 다 못 잡음)
- **수치 tweak 의 *최종 OK*** — SO 값 변경은 Inspector / 디버그 HUD, 사용자가 *느낌* 확인

자동화 영역 = *기계적 검증* (회귀 / 룰 grep / 씬 정합성 / 컴파일). *비전 결정 / "어색한 거 발견"* 은 사용자 손 보존.

### 사례 / 패턴 (TASK-WM-073 — 검증 자동화 인프라)

#### 1. 새 .cs 작성 흐름

```
dotnet build (Unity 무관, 코드 컴파일) — 통과 검증
  ↓
mcpforunity://editor/state — is_compiling 끝까지 polling
  ↓
read_console(types=["error", "warning"], count=10, format="detailed") — Unity 측 에러 grep
  ↓
(사용자 검증 단계는 비전 / 비주얼 / 동작 일 때만)
```

`create_script` / `script_apply_edits` 가 자동으로 import + 컴파일 트리거 — `unity-refresh.ps1` 호출 불필요. 단 `dotnet build` 는 *Unity stuck state 무관* 이라 첫 검증은 여전히 그것 우선.

#### 2. 신규 매니저 / prefab 도입 흐름 (Bootstrap 패턴)

```
.cs 작성 (Singleton<T> 베이스 + DataSO 등) → dotnet build
  ↓
Bootstrap menu (.cs 의 [InitializeOnLoadMethod] 또는 EditorWindow) — prefab/asset 자동 생성
  ↓
unity-refresh.ps1 (Bootstrap 트리거)  또는  Unity Editor focus
  ↓
read_console (Bootstrap 로그 + error 0 검증)
  ↓
find_gameobjects(search_term="<매니저이름>", search_method="by_component") — 등록 검증
  ↓
mcpforunity://scene/gameobject/{id} — 정확한 SerializeField 값 검증 (특히 dontDestroyOnLoad=true)
```

★ MCP `manage_gameobject create` 직접 사용 X — Bootstrap menu 패턴 우선 (수치 노출 / 런타임 tweak 룰 정합). MCP write 는 *디버그 / 일회성 시각 검증* 만.

#### 3. 씬 정합성 자동 검증 (NULL ref / 등록 누락)

```
find_gameobjects(search_method="by_component", search_term="MissingScript")  → 결과 0 검증
find_gameobjects(search_term="<핵심 매니저들>") → SOManager / EventBus / WorldClock / WeatherDirector / GameModeManager 등 모두 존재 검증
mcpforunity://scene/cameras → 카메라 셋업 (TASK-WM-056-F Camera IoC 검증)
mcpforunity://scene/volumes → URP Volume Profile (Sky / Weather)
```

특히 **WM-056-F IoC 마이그레이션 진행 중** 이라 Singleton 폐기 후속 worktree 마다 검증 필요 — `find_gameobjects` 자동.

#### 4. 시각 회귀 (스크린샷)

```python
# 빠른 시각 검증 — AI 가 스크린샷 보고 자연어 비교
manage_camera(action="screenshot", camera="MainCamera", include_image=True, max_resolution=512)

# 6각도 contact sheet (씬 전체 overview)
manage_camera(action="screenshot", batch="surround", max_resolution=256)

# Scene View (gizmo / wireframe / debug overlay 포함)
manage_camera(action="screenshot", capture_source="scene_view", view_target="<핵심 GameObject>", include_image=True)
```

baseline 비교 = 후속 (TASK-WM-073 sub-C). 지금은 AI 자연어 비교 ("baseline 과 다른가?") default — *비전 결정 / "어색한 거 발견"* 은 사용자 손 보존.

#### 5. EditMode / PlayMode 테스트 (TASK-WM-073 sub-D, 후속)

```python
# 신규 시스템 도입 시 첫 테스트 박는 패턴
run_tests(mode="EditMode", test_names=["TestSomething"])
result = get_test_job(job_id=..., wait_timeout=60, include_failed_tests=True)
```

WM 에 `com.unity.test-framework 1.6.0` 박혀있음. 현재 테스트 코드 0 — sub-D 시점에 첫 테스트 박힘 (`Variable<T>` / `WorldClock` / `EventBus` 후보).

#### 6. 작업 완료 보고 흐름 (MCP 도입 후 갱신)

```
1. 코드 변경 (create_script / Edit)
2. dotnet build — 통과 검증
3. (Unity 트리거 파일 — .meta / .prefab / .asset / 씬) unity-refresh.ps1 + sleep
4. read_console — error / warning grep
5. find_gameobjects — 씬 정합성 (해당 시)
6. (시각 검증 필요 시) manage_camera screenshot — AI 자연어 비교
7. 통과 시 사용자에게 *비전 / 비주얼 / 동작* 컨펌 요청 (남은 사용자 손)
8. 사용자 OK → commit
```

#### 7. Multi-instance + worktree race

여러 Unity Editor 띄울 때 (TASK-WM-069 인프라):
```
mcpforunity://instances → 활성 Editor 목록
set_active_instance(instance="WitchMendokusai@<hash>")  # main worktree
set_active_instance(instance="<branch>@<hash>")          # sub worktree
```

각 작업이 자기 worktree 의 Editor 만 조작 — 다른 세션 영역 침범 X.

## Git Workflow

본 레포 git workflow 정본은 **`wm-git-workflow` skill** (canonical: `memo/dotfiles/claude-skills/wm-git-workflow/SKILL.md`, deployed: `~/.claude/skills/wm-git-workflow/SKILL.md`). commit / push / release / worktree / audit / branch / tag 작업 시 자동 매칭 로드.

### Critical guard (매 세션 항상 의식)

- ★ **Trunk-based main 직접 push (default)** — PR 안 만듦. self-verify (`dotnet build` + 룰 검토) 통과 시 main 직접 (TASK-WM-063, 2026-05-09).
- ★ **Multi 세션 race — worktree 우회 강제** — `memo/.claude/active-sessions.md` 다른 행과 영역 겹침 시 *처음부터* worktree 안에서 작성. main local commit 후 cherry-pick hybrid 흐름 ❌ (동일 patch 다른 hash → 다이버전스 자기 자초).
- ★ **Push 직전 race 회피** — `powershell -File memo/dotfiles/scripts/safe-push.ps1 -Branch main` (fetch + merge + push retry).
- ★ **Force push 절대 금지** — `--force` / `-f` / `--force-with-lease` 모두 X. fast-forward only.
- ★ **Autopilot 한정 예외** — 자율 모드는 main 직접 push 금지, feature 브랜치 + Draft PR 까지만 (TASK-WM-063 sub-H 옵션 B).
- ★ **Unity 자연 단위 commit** — `.cs` + 자동생성 `.meta` + 의존 `.asset` / 씬 / `.prefab` 묶어 한 commit (분리 = 빌드 깨짐 / pull race).
- ★ **Conventional Commits + 한 commit 한 주제** — `feat: / fix: / chore: / refactor: / docs: / style:`. PR 폐기로 단위 자유도 ↑, 더 잘게.

세부 (worktree persistent scratch 패턴 / claude-audit POC v1-v4 검증 / Branch Protection 폐기 1회용 gh api / Release flow Tag-only `release.yml` / CHANGELOG 구조 / `Closes #NN` Issue 자동 종료 / CodeRabbit historical / Post-push 정리 / Tag↔bundleVersion drift 등) = wm-git-workflow skill 참고.

