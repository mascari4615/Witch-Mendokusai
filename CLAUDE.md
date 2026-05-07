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

## 컴파일 에러 확인 — 사용자 검증 요청 전 본인 먼저

코드 작성 후 사용자에게 "검증해주세요" 요청 *전*에 본인이 먼저 컴파일 에러 확인.

- Editor.log 위치: `C:\Users\masca\AppData\Local\Unity\Editor\Editor.log`
- `grep "error CS" Editor.log` 으로 컴파일 에러 검색
- 사용자가 코드 보고 검증하기 전에 *컴파일 통과* 자체가 사전 조건
- "사용자에게 빨리 넘기기" 보다 *검증 가능한 상태로 넘기기* 가 우선

### Editor.log stale 체크 — focus 안 됐으면 신뢰 X

Unity Editor 가 **foreground (focus)** 상태가 아니면 .cs 변경 reimport 안 함 → Editor.log 가 *내 최근 변경을 반영 안 한 상태*. "에러 없어 보임" 이 거짓일 수 있음.

확인 방법:
- 새로 만든 *심볼* (클래스명 / 메서드명 / enum 항목) 이 Editor.log 에 잡히는지 grep — 안 잡히면 reimport 안 됨
- `Reloading assemblies after forced synchronous recompile.` 로그의 *시점* — 내 변경 후인지 확인
- `Refresh completed in ...` 으로 import 세션 끝 시점 확인

stale 이면:
- 사용자에게 **Unity Editor focus 명시 요청** — "Editor 창 클릭해서 자동 reimport 시켜주세요"
- 또는 `Ctrl+R` (Refresh) 안내
- reimport 후 다시 grep "error CS" 로 본인 검증

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

## Git Workflow

본 § 가 본 레포 git workflow 의 정본. CodeRabbit 이 자동 픽업해 같은 룰로 PR 리뷰.

### 브랜치 + PR 강제 (AI Native 게이트)

`main` 직접 push 금지. 작업마다 브랜치 분기 후 PR.

- `feature/<주제>` — 새 기능
- `fix/<주제>` — 버그 fix
- `chore/<주제>` — 빌드·CI·의존성·환경 설정·문서
- `refactor/<주제>` — 동작 변화 없는 정리

**첫 커밋 시 바로 Draft PR 생성** + `.github/pull_request_template.md` 의도 채움 →
작업 진행하며 push → CodeRabbit 코멘트 대응 → **「§ Ready 전환 + auto-merge」 판단표 따라 Ready 자동 전환** (작은·격리 변경) 또는 Draft 유지 (큰·멀티 / 게이트 fail).

### Ready 전환 + auto-merge

`.github/workflows/auto-merge.yml` 가 `on: ready_for_review` + `if: draft == false` → `gh pr merge --auto --squash` 자동 호출. **Ready 전환만 되면 게이트 통과 후 squash 머지**. 따라서 모든 PR 을 Draft 로 두면 적체 — autopilot 룰 정본은 `karmoddrine/memo/dotfiles/claude-commands/autopilot.md` § Ready 전환 + auto-merge 게이트.

판단표 (모두 충족 시 Ready 자동):
- 변경 ≤ 150 LOC
- 단일 sub TASK
- 게이트 통과 (Code Quality + Unity build gate, `error CS` 0)
- stack 의존 base 없음 (또는 base 가 main)
- 새 인터페이스 = 첫 사용처 동봉 (데드 인터페이스 회피)
- Test plan 의 "사용자 검증 필요" 항목 0

호출:
```bash
gh pr ready <num>
gh pr merge --auto --squash <num>   # idempotent — auto-merge.yml 이 중복 호출해도 OK
```

미충족 시 PR description 에 "**Draft 사유**: <한 줄>" 명시 후 Draft 유지.

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

### Commit 메시지

Conventional Commits — `feat: / fix: / chore: / refactor: / docs: / style: / test: / perf:`.
한 commit 한 주제. 메시지 = 실제 변경 일치.

### Branch Protection (사용자 GitHub 측 설정)

룰을 *기계적으로 강제* 하려면 GitHub repo → Settings → Branches 에서 `main` 에 다음 protection rule:
- Require a pull request before merging
- Require status checks to pass:
  - `Check Typos` (현재 등록됨, 단 `continue-on-error: true` — 사실상 게이트 0. workflow 에서 `continue-on-error` 제거 필요)
  - `EditMode tests (compile gate)` — TASK-WM-047 (PR #89) 머지 + Unity license secrets (`UNITY_LICENSE`/`UNITY_EMAIL`/`UNITY_PASSWORD`) 등록 후 추가
- Restrict who can push to matching branches (직접 push 차단)

이 설정 안 되어있으면 본 § 룰은 *수동 약속* 만 됨. *auto-merge* 도 게이트가 약하면 위험 — Unity build gate 머지 후 protection 강화 필수.

설정 명령 (사용자 실행, 게이트 등록 후):

```bash
gh api -X PUT repos/Mascari4615/Witch-Mendokusai/branches/main/protection \
  -f required_status_checks.strict=true \
  -F 'required_status_checks.contexts[]=Check Typos' \
  -F 'required_status_checks.contexts[]=EditMode tests (compile gate)' \
  -F enforce_admins=false \
  -F required_pull_request_reviews=null \
  -F restrictions=null
```
