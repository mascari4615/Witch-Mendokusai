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

본 § 가 본 레포 git workflow 의 정본. **2026-05-09 TASK-WM-063 으로 trunk-based AI Native 로 전환** — PR / auto-merge / auto-rebase 폐기, claude-review (PR-bound) → claude-audit (post-push) 전환.

### Trunk-based — main 직접 push (default)

`main` 직접 push 가 *기본*. 모든 작업 (feat / fix / chore / refactor / docs) 모두 main 직접. PR 자체 만들지 X.

판단 기준 = *작성자 self-verify* 가능한지:
- ✅ Claude (또는 사용자) 가 `dotnet build Assembly-CSharp.csproj` 통과 + WM 룰 grep 통과 + 룰 본문 위배 검토 끝 → main 직접 push
- ⏸ 컴파일 안 되거나 룰 위배 의심 → push 보류, fix 후 재시도

이유: 솔로 indie + Claude 가 코드 작성자라 PR-bound review hook = self-review (작성자 = 리뷰어) 가치 약함. 사용자 시각 검증 시점은 main pull → Unity Play 단계 (PR 단계 아님). PR 1회 비용 (~3-5분 idle) + auto-rebase cascade 폭발 자체 제거.

### Worktree 우회 패턴 — 멀티 세션 보호

main worktree 는 *공유 검증 베이스* (다른 세션 dirty 와 본인 검증 의도 충돌). 따라서 main 직접 push 라도 main worktree 에서 직접 commit X — 항상 worktree 우회:

```bash
git worktree add -b chore/<주제> ../.worktrees/<name> origin/main
# <name> 에서 편집 + commit
git push origin chore/<주제>:main      # 로컬 브랜치 → 원격 main 직접 푸시
git worktree remove ../.worktrees/<name>
git branch -D chore/<주제>
```

브랜치 이름 prefix:
- `feat/<주제>` — 새 기능
- `fix/<주제>` — 버그 fix
- `chore/<주제>` — 빌드·CI·의존성·환경 설정·문서
- `refactor/<주제>` — 동작 변화 없는 정리

브랜치는 *임시 staging* — push 후 즉시 삭제. PR 만들지 X.

### Post-push audit (claude-audit)

`main` 에 push 가 발생하면 `.github/workflows/claude.yml` 의 `claude-audit` job 이 자동 트리거 (`on: push: branches: [main]`).

```
main push
  ↓
claude-audit job (anthropics/claude-code-action@v1, mode=agent)
  ├─ git show HEAD --stat + git diff HEAD~1 HEAD 분석
  ├─ 검토 영역: 버그 / 보안 / 성능 / WM 룰 (Allman / == false / var 금지 / FastFail / 수치 SO 노출 / InputManager / MenuItem WM/)
  └─ 결과:
       ├─ 정상 → no-op (Issue 생성 X)
       └─ 이상 발견 → gh issue create
            ├─ title: [main audit] <commit subject>
            ├─ label: audit, trunk-based
            └─ body: 발견된 문제 + 영향 + revert 제안 (옵션 A) 또는 후속 fix commit 제안 (옵션 B)
```

**사용자 응답 흐름**:
- Issue 받음 → 검토 → 옵션 선택
- 옵션 A (revert): `git revert <sha> && git push origin main`
- 옵션 B (fix commit): worktree 에서 fix → main 직접 push (그 fix commit 도 다시 audit 받음)
- Issue 코멘트에 `@claude 이거 fix 해` → claude.yml 의 `claude` job 트리거 → 자동 fix

**audit 원칙** (claude-audit prompt 본문):
- Actionable 한 지적만 — nitpick / 주관 X
- 큰 아키텍처 지적 = Issue 본문 「영향」 에만 (revert 제안 X — architectural fix 는 후속 TASK)
- 의심 약함 = Issue 생성 X (false positive 비용 ↑)
- chore / docs / 룰 본문 변경 = 룰 grep 면제

### Autopilot 예외 — Draft PR 유지 (TASK-WM-063 sub-H 옵션 B)

자율 모드 (`~/.claude/commands/autopilot.md`) 는 trunk-based 적용 *제외*. 자율 모드는 *사용자 인터럽트 X 환경* 이라 main 직접 push 면 사용자 잠든 사이 main 회귀 위험 ↑.

자율 모드 룰:
- `master`/`main` 직접 commit 금지 (autopilot 한정)
- feature 브랜치 + Draft PR 까지만 (사용자 검토 슬롯 보존)
- merge / push --force 금지

→ 자율 모드 PR 은 사용자 검토 후 *수동* 머지. 자율 모드 PR 도 main 머지 시 claude-audit 트리거 (정합).

### Commit 메시지

Conventional Commits — `feat: / fix: / chore: / refactor: / docs: / style: / test: / perf:`. 한 commit 한 주제. 메시지 = 실제 변경 일치.

PR 폐기로 *commit 단위 자유도 ↑* — PR 단위 묶음 강제 사라져 더 잘게 쪼갤 가치 ↑. 1줄 fix 도 별 commit 정합. 하루 commit 수 증가는 cascade 정합 (claude-audit 가 commit 별 독립 실행).

#### Closes #NN — Issue 자동 종료

post-push audit Issue 또는 사용자 발견 Issue 와 1:1 매핑이면 commit 메시지 본문 마지막에 `Closes #NN` 박는다 — main push 시 GitHub 가 Issue 자동 close.

매핑 없으면 박지 X (스팸).

### Branch Protection — 직접 push 허용

GitHub repo Settings > Branches 의 main 룰:
- ❌ Require a pull request before merging — *해제* (PR 폐기)
- ❌ Required status checks — *해제* (post-push audit 가 게이트 역할)
- ❌ Restrict who can push to matching branches — *해제* (직접 push 허용)
- ✅ Force push 차단 — 유지 (안전)

설정 명령 (사용자 1회):

```bash
gh api -X DELETE repos/Mascari4615/Witch-Mendokusai/branches/main/protection
# 또는 GitHub UI 에서 룰 자체 삭제
```

(이전 룰의 `gh api -X PUT ... required_status_checks ...` 명령은 폐기.)

### Post-push 정리

`delete_branch_on_merge: true` (repo 설정 — 자동 삭제, PR merge 흐름 잔재라 trunk-based 에서 무관). 로컬 잔여 worktree/branch 정리:

```bash
git fetch -p
git worktree list                       # 활성 worktree 확인
git worktree remove ../.worktrees/<name>  # 끝난 worktree 정리
git branch -D <임시 브랜치>               # push 후 임시 브랜치 삭제
```

### C# 컴파일 검증 — *로컬 검증 (CI 게이트 X)*

(TASK-WM-060, 2026-05-08 도입 → 같은 날 폐기 — public repo + self-hosted runner 보안 위험. § 본문은 본 레포 § 컴파일 에러 확인 — `dotnet build` 우선 참고.)

claude-audit 은 *룰/논리/보안* 검토만 — *컴파일* 검증은 X (worktree 안 dotnet build 가 작성자 책임).

### CodeRabbit / Copilot — 자동 review 비활성화 (TASK-WM-062 sub-G, 2026-05-08)

`.coderabbit.yaml` `auto_review.enabled: false` + GitHub repo Settings > Code review > Copilot review off. PR 폐기로 secondary AI review 자체 호출 시점 사라짐 — 룰 본문은 historical reference.

후속 활용 (재활성화 검토):
- 수동 invoke (PR 코멘트 `@coderabbitai review`) 는 PR 부활 시점 — 본 단계엔 의미 X
- claude-audit 결과 보강용 secondary audit — 비용 대비 가치 평가 후 도입

### Release branch + 통합 PR — *후속 단계 검토 (현 단계 도입 X)*

사용자 idea (TASK-WM-063 시드, 2026-05-09): "릴리즈 브랜치 같은 거에 합칠 때 PR로 통합 검토".

WM = early dev (Steam/itch.io 발행 사이클 없음) 라 release branch 도입은 의미 약함 (release = de facto main). publish 사이클 도달 시 별도 TASK 로 진입:
- `release/v0.x` 브랜치 + tag 자동화
- `main → release` 통합 PR — milestone 단위 review
- 그 시점에 본 § 추가
