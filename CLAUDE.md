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
- 한 파일 500줄 상한. 넘으면 상황을 보고 판단한다 (사용자 2026-09-06). 관심사가 여럿이면 같은 클래스는 partial 로 관심사별 파일, 아니면 클래스를 나눈다. 직렬화 자료 정의처럼 한 덩어리로 읽어야 하는 것은 기준선에 사유를 적고 둔다 (`TowerDefenseStageSO`). 규칙 게이트 `FILE-LENGTH` 가 새로 넘는 파일을 막고, 이미 넘은 것은 `wm-file-length-baseline.tsv` 의 빚 (줄어들기만 한다. 2026-09-06 에 1 파일, `TowerDefenseStageSO` 직렬화 자료). 가르기는 `python memo/dotfiles/scripts/wm-split-partial.py <file> --list` 로 멤버를 보고 `--plan` 으로 (같은 클래스 partial, .meta 포함), 한 파일에 타입이 여럿이면 `--extract`. 소스 경로를 글자로 읽는 시험이 있으면 `이름*.cs` 로 이어 읽게 한다

## DomainSDK / Mods SDK

절차 (격상 순서, Bridge 패턴, 참조 최소화 명령, 함정) 는 Skill `wm-domain-sdk`. 비전은 `memo/wm/design/vision/architecture.md`. 여기는 계약.

- DomainSDK 는 `WitchMendokusai/DomainSDK/` (루트, 로컬 UPM). 조각 전부 `noEngineReferences: true`, 루트 asmdef 없음. 소비자는 실제 쓰는 조각만 참조
- 엔진 다리는 Core 한 곳 (`NumericsUnityBridge`, `MessagePipeEventTransport`). SDK 안 `#if UNITY` 금지
- DomainSDK 에서 Core Singleton 직접 호출 금지. `IXxxBridge` + `XxxBridge.Register`
- Mods 의 references 는 DomainSDK 조각만
- 새 매니저나 시스템 전에 기존 패턴 (`Singleton<T>`, `OnXxxChanged`, `SOManager.DataSOs`) 먼저. 다른 모양이면 TASK 시드에 이유

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

## 컴파일 검증과 Unity 통로

절차 (검사 순서, 명령, 켜기, 캡처, 녹화) 는 Skill `unity` 의 WM 절. 룰 계약은 `memo/rules/unity.md`. 여기는 WM 계약만.

- **컴파일 검증 1순위는 `wm-compile-check.ps1 -ProjectPath <검사할 checkout>`** (5초, 에디터와 CLI 무관). 에디터 실컴파일이 최종. `dotnet build` 직접 호출 폐기, Editor.log 는 fallback only
- **warning 0 은 기계가 강제** (TASK-WM-204). 자기 asmdef 폴더의 `csc.rsp` 에 `-warnaserror+`. 보존 의도만 `#pragma warning disable` + 사유 주석. `Assets/csc.rsp` 는 만들지 않는다 (서드파티가 섞임)
- `csc.rsp` 는 ASCII 플래그만
- `run_tests` 는 `--async_tests true` 필수. 전체 스위트는 살아있는 에디터에서 완주 못 한다
- 에디터 꺼져 있으면 자동 기동. 사용자에게 "켜주세요" 안 한다

## Git Workflow

정본 = **`wm-git-workflow` skill** (commit / push / worktree / release / audit 전부). trunk-based main 직접 push, force push 절대 금지, multi-세션 race 시 worktree 격리.
