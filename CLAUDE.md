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
