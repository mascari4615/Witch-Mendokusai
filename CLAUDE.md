# WitchMendokusai — AI 에이전트 작업 지침

> Claude Code 자동 로드용 호환 문서다. 세계관 = `memo/CLAUDE.md`. 상세 워크플로 = `memo/wm/` 및 `WitchMendokusai/docs/`.

## 입력 처리

### 규칙

- **New Input System(`UnityEngine.InputSystem`)만 사용** — 레거시 `Input.*` API 절대 금지
- **게임 컴포넌트에서 `Keyboard.current` / `Mouse.current` 직접 접근 금지** (InputManager 내부·연속값 읽기 예외)
- **모든 입력 이벤트는 `InputManager.RegisterInputEvent`** 를 통해 등록·해제

### 아키텍처

```
WMInput.inputactions → InputManager.BindEvents() → On{Start/Performed/Canceled} → RegisterInputEvent 콜백
```

관련 파일: `Assets/_WitchMendokusai/Core/Scripts/Input/` (InputManager.cs / WMInput.inputactions / InputStrategy/)

### 새 입력 이벤트 추가 — 3곳 동시 수정

추가 전에 키 중복부터 확인한다.

- `WMInput.inputactions`의 `"path": "<Keyboard>/..."` 바인딩 전체를 확인한다.
- `InputManager`가 연속값으로 직접 읽는 이동·카메라 키도 함께 확인한다.
- 두 곳 모두와 충돌하지 않는 키를 고른 뒤 아래 세 파일을 수정한다.

1. `InputManager.cs` — `InputEventType` 열거형 항목 추가
2. `InputManager.cs` — `inputEventBindings` 딕셔너리에 ActionMap 연결
3. `WMInput.inputactions` — 해당 ActionMap에 Action 추가 (name = InputEventType 문자열, path = `"<Keyboard>/x"` 등)

`InputEventResponseType`: `Started`(누르는 순간) / `Performed`(완료, 단발 액션 default) / `Canceled`(뗄 때) / `Get`(매 프레임 지속).

씬 전환 시 InputManager 가 `SetInputStrategy` 로 이벤트 초기화 → `Start` 에서 직접 Register 한 컴포넌트는 `OnDestroy` 에서 반드시 Unregister.

## 코딩 스타일

- `var` 금지 — 항상 명시적 타입
- 변수명 축약 금지 — `t`/`r`/`e` 대신 전체 이름 (`inputEventType` 등). 루프 인덱스 `i`/`j` 예외
- 상수 `UPPER_SNAKE_CASE`
- 부정 조건 `== false` — `!` 금지
- Allman 스타일 (중괄호 항상 새 줄), 단일 표현식은 `=>` expression body
- 이벤트/델리게이트 초기값 `delegate { }` (null 방지)

## 새 시스템 도입 시 — 기존 패턴 먼저

새 매니저/시스템 전에 `Singleton<T>` 상속·`OnXxxChanged` 이벤트·`SOManager.DataSOs`·`GameModeManager.OnModeChanged` 구독 패턴 확인. 다른 모양이면 TASK 시드에 이유 명시.

## DomainSDK / Mods SDK — 6 동기 first-use 패턴

비전 정본: `memo/wm/design/vision/architecture.md`. 본 § 는 코드 룰.

**asmdef 단방향**: `WitchMendokusai.DomainSDK.asmdef` references=[UniTask, MessagePipe] (Foundation 유틸만, 게임 layer 0) / Mods references=[DomainSDK]만. 모드·DomainSDK .cs 가 Domain/Core 타입 직접 호출 시 컴파일 fail = 런타임 체크 0. (구 "references=[]" = stale. 실 도메인 격상 현황·입도 정책 = `memo/wm/design/vision/architecture.md` § 측정표·격상 입도 정책.)

**격상 순서**: `enum` → `SaveData`(POCO) → `InfoData`(POCO) → `RuntimeXxxSaveData` → `record XxxEvent : IEvent` → `RuntimeXxx` → asmdef split. RuntimeXxx 생성자 = `(RuntimeXxxSaveData)` 만, Domain factory(`FromXxxSO`/`FromXxxInfo`/`FromSaveData`)가 변환 책임.

**Bridge 패턴** (DomainSDK → Core Singleton 호출 금지): DomainSDK 안 `IXxxBridge` interface + `XxxBridge` static accessor, Core 매니저가 `Awake` 에 `XxxBridge.Register(this)`. null check 제거(FastFail — Bootstrap 후 호출 보장).

**Mods SDK 진입점**: `DomainSDK/Mods/IMod.cs` (Name/Version/Initialize, Unity 의존 0) + `Domain/Mods/ModLoader.cs` (AfterAssembliesLoaded reflection 발견 + Initialize).

**격상 주의**: Unity 6.x csproj stale(신규 .cs 후 CS0246 지속 → Editor 재시작) / git mv + push race(옛 위치 재등장 → worktree 사용) / fsnotify 누락(신규 폴더 다중 파일 → Assets > Refresh).

## Editor 메뉴

`MenuItem` top-level root = **`WM/`** 단일화. `WitchMendokusai/...` 사용 X. grep 게이트: `MenuItem.*"WitchMendokusai/` 결과 0.

## 수치 노출 / 런타임 tweak

모든 수치·시간·길이·가중치·확률 하드코딩 금지. SO / `[SerializeField]` / `Variable<T>` 노출, 매니저는 SO 값 캐싱 X(매 사용 시 read). 같은 수치 두 곳 박기 X. MCP `manage_components.set_property` 로 수치 변경 시 SO 정본 우회 위험 — 디버그 외 사용 X.

### 코드로 짓는 UIToolkit 은 USS 로 (TASK-WM-206)

`[SerializeField]` 는 MonoBehaviour 에만 붙는다. 코드로 짓는 순수 C# `VisualElement` 클래스
(`TacticEditorView` / `EdgeRuntimeElement` 등)의 **색·간격·글자 크기는 USS 로 내린다.**

- 스타일시트는 **`[SerializeField] StyleSheet` 로 받아 `styleSheets.Add`** — `UIRoot` 선례.
  `Resources.Load<StyleSheet>("문자열")` 신규 사용 X (경로 오타 시 조용히 null → 그냥 못생기게 뜬다).
- 패널 클래스는 스타일시트를 **자기가 로드하지 않는다.** 마운트하는 MonoBehaviour 가 붙여준다
  (그래야 누가 무슨 스타일을 쓰는지 인스펙터에 보인다). 클래스는 USS 클래스 이름만 안다.
- 값이 아니라 *의미* 로 이름 짓는다(`--wm-panel-bg`) — 팔레트가 한 자리에 산다.

⚠ 색을 `static readonly` 로 옮기는 절반짜리는 룰을 못 채운다 — 리터럴만 이사할 뿐 런타임 tweak 은
여전히 불가. 그건 「겉만 깨끗해진 것」이다.

## 에러 처리 — FastFail 유지

방어 코드(TryGet/null체크/기본값 반환)로 증상 덮지 말고, 등록 누락 등 근본 원인 고침. `[]` 직접 접근 등 FastFail 메서드 그대로 유지.

## 객체 참조 획득 — init-order 안전 규약

**단일 안티패턴**: Awake/`[Inject] Construct` 에서 아직 생성·등록 안 된 대상을 eager Find/Inject → null 영구 고정.

**금지 / 대체**:
1. `Awake`/`Construct` 에서 `FindAnyObjectByType<T>` / `FindObjectsByType<T>` 금지 → ① 사용 시점 lazy resolve(`EnsureX()` 멱등) ② 소유자 push ③ DI `[Inject]`
2. `container.Inject(component)` 금지 → `container.InjectGameObject(go)` (자식·형제 재귀)
3. 준비 안 된 값 스냅샷 캐싱 금지 → live 파생 프로퍼티 (`=> source?.Value`)

**게이트**: `.github/scripts/wm-init-order-audit.ps1` — [BLOCK](exit 1): Awake 안 Find → root fix 또는 `// init-order-ok`. [ORDER-RISK]: Start/OnEnable 안 cross-ref Find → lazy/owner-push/scope 결정합성, 적용외면 `// init-order-ok` + 사유. [REVIEW]: `container.Inject(` → InjectGameObject 검토. PR 시 BLOCK 0 + ORDER-RISK 0(또는 정당화) 확인.

면제 마커 `// init-order-ok: <사유>` 는 **그 줄** 또는 **메서드 시그니처 줄·바로 위 주석 블록**에 둔다 (메서드 스코프). 코드를 만나면 거슬러 올라가기를 멈추므로 파일 전체로 새지 않는다.

**게이트를 고쳤으면 `-SelfTest` 를 돌려라** (TASK-WM-211). 표본(`.github/scripts/fixtures/init-order/`)으로 *잡을 것을 잡고 면제할 것을 면제하는지* 검사한다. 이 검사가 없던 동안, 패턴이 `FindAnyObjectByType` 을 못 잡는 채로 몇 달간 「위반 0 / PASS」 였다 — **초록이 「위반 없음」이 아니라 「안 봤음」을 뜻할 수 있다.**

**`Singleton<T>` dontDestroyOnLoad** = prefab SerializeField 정본(코드 `DontDestroyOnLoad()` 강제 호출 X).

## 컴파일 검증 — MCP `read_console` 정본

- **정본 = `read_console(types=["error","warning"], count=30)`** — Mono runtime 현재 Console 직접. error + warning 둘 다 0 = 클린.
- **`dotnet build` 폐기** — Unity Mono ≠ .NET 8, false confidence. wrapper(`dotnet-build-wm.ps1`) 경유 시만 허용.
- **Editor.log = fallback only** — append-only 누적으로 옛 컴파일 결과 섞임. MCP 가용 시 절대 사용 X.
- Warning = 미래 error 시그널. error 0 만 보고 통과 X. 보존 의도 warning은 `#pragma warning disable` + 사유 주석.

**warning 0 은 이제 기계가 강제한다 (TASK-WM-204).** WM 자기 asmdef 8개 폴더마다 `csc.rsp` =
`-warnaserror+`. 경고가 곧 컴파일 에러라 *다음 줄을 못 쓴다* → 미루는 것 자체가 불가능.
패키지·서드파티는 각자 컴파일이라 무관(`Assets/csc.rsp` 는 만들지 X — predefined 어셈블리에 서드파티가 섞임).

- **탈출구**: 보존 의도 = `#pragma warning disable <ID>` + 사유 주석. 유니티 업그레이드가 새 폐기
  경고를 쏟아 전면 RED 면 해당 `csc.rsp` 한 줄 주석 처리로 즉시 원복(비가역 0) 후 TASK 로 소화.
- **`csc.rsp` 는 ASCII·플래그만.** 주석·한글 넣으면 PS/cp949 경로에서 깨져 인자로 먹혀 `CS2001`
  (실측 2026-08-05). 근거는 본 문서에 적고 파일엔 플래그만.

**컴파일 트리거**: `refresh_unity(mode="force", scope="all", compile="request", wait_for_ready=true)` 또는 fallback `unity-refresh.ps1`.

## Unity-MCP layer

**정본 = CoplayDev `com.coplaydev.unity-mcp`** (MIT, Unity Cloud cap 무관). 포트 **12345** (`.mcp.json` = `http://127.0.0.1:12345/mcp`), type=`"http"` 필수.

**Editor 꺼져있으면 자동 기동** — WM 작업(특히 behavior-verify/Play) 요청 시 Editor 가 죽어있어도 사용자에게 "켜주세요" 푸시백 X. `memo/dotfiles/scripts/ensure-unity-editor.ps1` 호출 = heavy-op preflight → `Unity.exe -projectPath`(버전 자동 감지) → MCP 포트 12345 listen 대기 → ready. 정본 = TASK-KAR-159 + 메모리 `[[wm-request-auto-launch-unity-mcp]]`.

**Read (자율)**: `read_console` / `mcpforunity://editor/state` / `find_gameobjects` / `manage_camera(screenshot)` / `run_tests(EditMode)` / `unity_reflect` / `unity_docs`.

**Write (신중)**: `create_script` / `script_apply_edits` / `manage_gameobject` / `manage_components.set_property`(수치 노출 룰 위반 위험) / `manage_assets` / `manage_prefabs`.

**PlayMode 테스트**: MCP `run_tests(PlayMode)` = WM heavy-boot 비정본(15s init-cap + HTTP 브릿지 정지 = wedge). 부팅 회귀 = `wm-boot-smoke.ps1`, DI-graph = `wm-editmode-smoke.ps1`.

**작업 완료 흐름**:
1. 코드 변경 → `refresh_unity` or `create_script`(자동 reimport)
2. `mcpforunity://editor/state` is_compiling 끝까지 polling
3. `read_console(types=["error","warning"])` — 0 entries 검증
4. fail → 즉시 fix → 2. 반복
5. 통과 → 사용자에게 비전/비주얼/동작 컨펌

**Multi-worktree**: `mcpforunity://instances` 목록 → `set_active_instance` 로 자기 worktree Editor 선택. `McpAutoBinder.cs`(`[InitializeOnLoad]`) 가 `.mcp.json` 자동 갱신.

## Git Workflow

정본 = **`wm-git-workflow` skill** (commit / push / worktree / release / audit 전부). trunk-based main 직접 push, force push 절대 금지, multi-세션 race 시 worktree 격리.
