---
applyTo: "**/*.cs"
---

# WitchMendokusai C# 코딩 룰

## 타입 및 변수명

- `var` 사용 금지 — 항상 명시적 타입으로 선언
- 변수명 축약 금지 — `t`, `r`, `e` 대신 `inputEventType`, `callbackContext` 등 풀네임
  - 예외: `for` 루프의 `i`, `j` 관용 인덱스
- 상수는 `UPPER_SNAKE_CASE`

## 불리언 비교

- 부정 조건은 `== false` 사용, `!` 연산자 금지
  - ✅ `if (IsValid() == false)`
  - ❌ `if (!IsValid())`

## 중괄호 (Allman 스타일)

- 중괄호는 항상 새 줄에
- 단일 표현식 메서드/프로퍼티는 `=>` expression body 허용

## 입력 시스템

- `Keyboard.current` / `Mouse.current` 게임 컴포넌트에서 직접 접근 금지
  - 허용 예외: `InputManager` 내부, 카메라 연속값
- 모든 입력 이벤트는 `InputManager.RegisterInputEvent` 경유

## 에러 처리

- 방어 코드(null 체크·TryGet·기본값 반환)로 증상 덮지 말 것 — 근본 원인 수정
- FastFail 유지 (`[]` 직접 접근 등)

## 리뷰 기준

- 위 룰 위반 → 지적
- 아키텍처·설계 변경 필요한 큰 이슈 → 코멘트만, 코드 변경 제안 X
- 주관적 스타일 의견 (명명 취향, 줄바꿈 개수 등) → 건너뜀
