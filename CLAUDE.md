# WitchMendokusai — AI 에이전트 작업 지침

> Claude Code 자동 로드용 호환 문서다. 세계관 = `memo/CLAUDE.md`. 상세 워크플로 = `memo/wm/`.

## 입력 처리

### 규칙

- **New Input System(`UnityEngine.InputSystem`)만 사용** — 레거시 `Input.*` API 절대 금지
- **게임 컴포넌트에서 `Keyboard.current` / `Mouse.current` 직접 접근 금지** (InputManager 내부·연속값 읽기 예외)
- **모든 입력 이벤트는 `InputManager.RegisterInputEvent`** 를 통해 등록·해제

### 아키텍처

```
WMInput.inputactions → InputManager.BindEvents() → On{Start/Performed/Canceled} → RegisterInputEvent 콜백
```

관련 파일: `Assets/_WitchMendokusai/Core/Input/` (InputManager.cs / WMInput.inputactions / InputStrategy/)

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
- 한 파일 500줄 상한. 넘으면 같은 클래스는 partial 로 관심사별 파일, 아니면 클래스를 나눈다. 규칙 게이트 `FILE-LENGTH` 가 새로 넘는 파일을 막고, 이미 넘은 40파일은 `wm-file-length-baseline.tsv` 의 빚 (줄어들기만 한다)

## 새 시스템 도입 시 — 기존 패턴 먼저

새 매니저/시스템 전에 `Singleton<T>` 상속·`OnXxxChanged` 이벤트·`SOManager.DataSOs`·`GameModeManager.OnModeChanged` 구독 패턴 확인. 다른 모양이면 TASK 시드에 이유 명시.

## DomainSDK / Mods SDK — 6 동기 first-use 패턴

비전 정본: `memo/wm/design/vision/architecture.md`. 본 § 는 코드 룰.

**위치**: DomainSDK 는 `WitchMendokusai/DomainSDK/` (레포 루트, `Packages/manifest.json` 의 `file:../DomainSDK` 로컬 UPM 패키지. 2026-09-05 이동). Assets 안이 아니다. 서버와 Portable 과 웹이 같은 폴더를 본다.

**asmdef 단방향**: DomainSDK 조각 36개 전부 `noEngineReferences: true`, Unity 계열 참조 없음. 루트 asmdef 없음 (2026-09-05 제거. 소비자는 필요한 조각을 하나씩 참조). 조각별 참조 검증은 `python memo/dotfiles/scripts/wm-sdk-asmdef-build.py` (asmdef 참조를 csproj 참조로 옮겨 조각마다 따로 굽는다, 에디터 불필요). 소비자 asmdef (Core, Domain, Network, Editor, Tests, Mods) 는 실제 쓰는 조각만 참조한다 (2026-09-05 실측 Core 47 -> 22, Network 37 -> 10, Editor 50 -> 30, Mods.Sample 34 -> 5). 재기는 `python memo/dotfiles/scripts/wm-asmdef-refs-audit.py <project> minimize <asmdef>` (참조 DLL 만으로 소비자를 굽고 컴파일러가 빠졌다는 것만 되돌린다). 새 조각을 쓰기 시작하면 references 에 그 조각 하나만 추가. 엔진 다리는 Core 한 곳: `Core/Numerics/NumericsUnityBridge.cs` 의 `ToUnity()`, `ToSim()` 확장과 `Domain/Application/DI/MessagePipeEventTransport.cs` (RootLifetimeScope 가 `EventBusBridge.UseTransport` 로 꽂음). SDK 안 `#if UNITY` 분기 없음. Mods 의 references 는 DomainSDK 조각만. 모드와 DomainSDK .cs 가 Domain/Core 타입을 직접 호출하면 컴파일이 막으므로 런타임 체크가 필요 없다. (실 도메인 격상 현황과 입도 정책은 `memo/wm/design/vision/architecture.md` 의 측정표와 격상 입도 정책 절.)

**격상 순서**: `enum` → `SaveData`(POCO) → `InfoData`(POCO) → `RuntimeXxxSaveData` → `record XxxEvent : IEvent` → `RuntimeXxx` → asmdef split. RuntimeXxx 생성자 = `(RuntimeXxxSaveData)` 만, Domain factory(`FromXxxSO`/`FromXxxInfo`/`FromSaveData`)가 변환 책임.

**Bridge 패턴** (DomainSDK → Core Singleton 호출 금지): DomainSDK 안 `IXxxBridge` interface + `XxxBridge` static accessor, Core 매니저가 `Awake` 에 `XxxBridge.Register(this)`. null check 제거(FastFail — Bootstrap 후 호출 보장).

**Mods SDK 진입점**: `DomainSDK/Mods/IMod.cs` (Name/Version/Initialize, Unity 의존 0) + `Domain/Mods/ModLoader.cs` (AfterAssembliesLoaded reflection 발견 + Initialize).

**격상 주의**: Unity 6.x csproj stale(신규 .cs 후 CS0246 지속 → Editor 재시작) / git mv + push race(옛 위치 재등장 → worktree 사용) / fsnotify 누락(신규 폴더 다중 파일 → Assets > Refresh).

## 폴더 규약 (2026-09-05)

- 코드는 Feature 폴더 바로 아래. 주제 하위 폴더 (`Quest/Objective/`) 는 허용, `Scripts` 층은 금지. 게이트 `FOLDER` (wm-rule-gate) 가 push 를 막는다
- Core 도 같은 꼴: `Core/Input/`, `Core/UI/`. 옛 `Core/Scripts/` 는 없다
- 자산과 코드를 가르는 건 폴더 이름이 아니라 확장자. Unity 가 뜻을 두는 폴더 이름 (`Resources`, `Editor`) 만 그 뜻으로 쓴다. UI 텍스처는 `Art/`
- `Domain/` 은 한 축: Feature 폴더 (`TowerDefense`, `Quest` ...) 와 이름 붙은 공용 모듈 폴더 (`GameData` SO 저장소와 DataManager, `Actor` 유닛과 이동과 BT 와 피격 반응, `Effect`, `Criteria`, `NodeGraph`, `Save`, `UGC`, `Pool`, `Behavior` 범용 MonoBehaviour, `UI` 셸과 공용 위젯, `Discovery`, `Hub`). 종류 이름 폴더 (`Data`, `Component`) 는 없다 (2026-09-05 해체). `Entry` 는 런타임 호출 0 인 삭제 후보
- Feature 고유 UI 는 그 Feature 안 (`Item/UI`, `Quest/UI`). `Domain/UI` 에는 여러 Feature 가 쓰는 것만
- 자산 하위 폴더 이름 통일 (`Assets`, `Content`, `Prefabs` 혼재) 은 남은 공백 (memo Change wm-code-structure)

## Editor 메뉴

`MenuItem` top-level root = **`WM/`** 단일화. `WitchMendokusai/...` 사용 X. grep 게이트: `MenuItem.*"WitchMendokusai/` 결과 0.

**메뉴 경로는 영문만. 한글 절대 금지** (사용자 2026-08-30 재지시. 정본 `memo/rules/unity.md § editor 메뉴`). 창 내용, 로그, 툴팁은 한국어 가능. 게이트 `MENU-ASCII` (wm-rule-gate) 가 push 를 막는다. 한글 표기가 필요하면 언어 설정을 따르는 로컬라이즈 기능으로 (메뉴에 직접 X).

## 수치 노출 / 런타임 tweak

모든 수치, 시간, 길이, 가중치, 확률 하드코딩 금지. SO / `[SerializeField]` / `Variable<T>` 노출, 매니저는 SO 값 캐싱 X(매 사용 시 read). 같은 수치 두 곳 박기 X. 자동화로 컴포넌트 값을 직접 바꿔 SO 정본을 우회하는 것은 디버그 외 사용 X.

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

## 컴파일 검증 1순위: `wm-compile-check.ps1` (에디터와 통로 무관)

**정본은 `powershell -File memo/dotfiles/scripts/wm-compile-check.ps1 -ProjectPath <검사할 checkout>`** (lane 이면 lane 경로 필수. 인자 이름이 틀리면 조용히 기본값인 공유 checkout 을 검사한다. 2026-08-30 실측: `-Repo` 로 여섯 번 초록을 받았는데 전부 다른 폴더였다). 진짜 Unity 어셈블리
(`Editor/Data/Managed/UnityEngine/*.dll` + `UnityEditor.dll` + `Library/ScriptAssemblies` + PackageCache/Assets 의
미리 컴파일된 DLL, 총 ~500 참조)를 걸고 우리 `.cs` 1400여 개를 한 번에 굽는다. **5초. 에디터가 프로젝트를
잠그고 있어도 돈다.** exit 0/1/2(2는 못 돌렸음, 에러 0과 다름).

- **왜 바뀌었나 (2026-08-16)**: 정본이 라이브 콘솔 하나였는데, 에디터 잠금 + 통로 변경이
  겹치자 검증 경로가 통째로 사라져 사람에게 "유니티 창 눌러 주세요"로 떠넘겨야 했다. 검증이 외부 통로
  하나에 묶여 있던 것 = 단일 실패점. 「`dotnet build` 폐기」의 근거는 *Mono ≠ .NET8 로 API 표면이 다르다*
  였는데, **엔진 DLL 자체를 참조하면 API 표면은 진짜다** — 그래서 이 경로만 예외로 승격한다.
- **못 잡는 것 (그래서 아래가 여전히 필요)**: asmdef 경계 위반(한 덩어리로 구움), 플랫폼/IL2CPP,
  에셋·직렬화·PlayMode 동작. 맨 소스만 참조하는 검사라 **에디터 실컴파일이 최종 확인**이다.
- **2순위: `unity command console --project-path <WM>`**, 서비스 가능할 때 warning 0까지 확인.
- **`dotnet build` 직접 호출은 여전히 폐기** — 위 스크립트/wrapper 경유만.
- **Editor.log는 fallback only.** append-only 누적으로 옛 컴파일 결과 섞임. CLI 가용 시 절대 사용 X.
- Warning = 미래 error 시그널. error 0 만 보고 통과 X. 보존 의도 warning은 `#pragma warning disable` + 사유 주석.

**warning 0 은 이제 기계가 강제한다 (TASK-WM-204).** WM 자기 asmdef 8개 폴더마다 `csc.rsp` =
`-warnaserror+`. 경고가 곧 컴파일 에러라 *다음 줄을 못 쓴다* → 미루는 것 자체가 불가능.
패키지·서드파티는 각자 컴파일이라 무관(`Assets/csc.rsp` 는 만들지 X — predefined 어셈블리에 서드파티가 섞임).

- **탈출구**: 보존 의도 = `#pragma warning disable <ID>` + 사유 주석. 유니티 업그레이드가 새 폐기
  경고를 쏟아 전면 RED 면 해당 `csc.rsp` 한 줄 주석 처리로 즉시 원복(비가역 0) 후 TASK 로 소화.
- **`csc.rsp` 는 ASCII·플래그만.** 주석·한글 넣으면 PS/cp949 경로에서 깨져 인자로 먹혀 `CS2001`
  (실측 2026-08-05). 근거는 본 문서에 적고 파일엔 플래그만.

**컴파일 트리거**: `refresh_unity(mode="force", scope="all", compile="request", wait_for_ready=true)` 또는 fallback `unity-refresh.ps1`.

## Unity 통로 — 공식 Unity CLI (Pipeline)

**룰 정본 = `memo/rules/unity.md § Unity 통로`.** 본 § = WM 레포 포인터.

공식 `unity` CLI + `com.unity.pipeline` 을 쓴다. 실측 근거는 `memo/notes/2026-08-20-unity-cli.md`에 있다.

```bash
unity status                          # 붙은 에디터 — 단, ready 를 믿지 마라(아래)
unity command eval "return 1;" --project-path <WM>    # ← 서비스 가능 판정은 이것으로만
unity command console --project-path <WM>             # 콘솔 읽기
unity command recompile / recompile_status
unity command editor_play / editor_stop / capture_game_view
unity command run_tests -- --mode editor --async_tests true --filter <이름조각>
```

**꼭 지킬 것 넷** (자세한 근거는 룰 정본):

1. `unity status` 의 `ready` ≠ 명령 받을 수 있음. WM 은 ready 뒤 **+20초** 503 을 냈다.
2. 무거운 순간(Play 부팅·도메인 리로드) 400/503 은 정상 — 재시도 5–10초, 최대 60초.
3. **메인 스레드 하드캡 5000ms** — 무거운 `eval` 금지, 쪼개거나 `--detach`.
4. **`run_tests` 는 반드시 `--async_tests true`.** 안 켜면 동기 모드가 메인 스레드를 잡고
   대기하다 타임아웃 취소와 데드락 → **에디터 강제 종료 + 재임포트**가 유일한 복구다.
   **WM 전체 스위트(1898개)는 살아있는 에디터에서 완주 못 한다** — 일상은 `--filter`.

**컴파일 검증 1순위는 그대로 `wm-compile-check.ps1`** (5초, 에디터 잠금과 CLI 상태 무관). CLI는
필수 경로가 아니다 — 그게 2026-08-16 에 배운 것이고 통로가 바뀌어도 유지된다.

**Editor 꺼져있으면 자동 기동** — 사용자에게 "켜주세요" 푸시백 X. `unity open <WM> --args "-automated"`
후 위 1번 방식으로 서비스 가능해질 때까지 폴링.

## Git Workflow

정본 = **`wm-git-workflow` skill** (commit / push / worktree / release / audit 전부). trunk-based main 직접 push, force push 절대 금지, multi-세션 race 시 worktree 격리.
