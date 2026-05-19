# WitchMendokusai — AI 에이전트 작업 지침

이 레포는 Unity 게임 WM의 코드 레포다. 기존 `CLAUDE.md`가 상세 작업 규칙의 정본이며, 이 파일은 Codex 같은 다른 에이전트가 들어올 때의 공용 입구다.

## 먼저 볼 문서

- 상세 작업 규칙: `CLAUDE.md`
- 루트/공통 룰: `../memo/UMBRELLA.md`, `../memo/rules/*.md`
- TASK 사양: `../memo/wm/tasks/`
- 설계 비전: `../memo/wm/design/vision/architecture.md`

## 핵심

- Unity New Input System만 사용한다.
- WM C# 스타일은 `CLAUDE.md`의 코딩 스타일을 따른다.
- 작업 중 사용자에게 필요한 Unity 에디터 작업은 해당 TASK 문서의 체크리스트에 즉시 기록한다.
- 새 Unity 파일을 추가하면 `.meta`까지 함께 추적한다.

## Git

이 폴더는 독립 git repo다. 코드 변경 커밋은 `WitchMendokusai/` repo에서 한다.
