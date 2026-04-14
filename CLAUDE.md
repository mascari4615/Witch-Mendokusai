# WitchMendokusai — Claude 작업 지침

## 입력 처리

Unity New Input System(`UnityEngine.InputSystem`)만 사용한다.
`Input.GetKeyDown`, `Input.GetAxis` 등 레거시 Input API는 사용하지 않는다.

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
