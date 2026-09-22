# WitchMendokusai — AI 에이전트 작업 지침

이 레포는 Unity 게임 WM의 코드 레포다. 공통 규칙은 `../memo/rules/`, WM 전용 코드 규칙은 `CLAUDE.md`를 따른다. 이 파일은 코딩 에이전트의 공용 입구다.

## 먼저 볼 문서

- 상세 작업 규칙: `CLAUDE.md`
- 모든 작업 필독: `../memo/UMBRELLA.md`, `../memo/rules/process.md`, `../memo/rules/git.md`, `../memo/rules/quality.md`, `../memo/rules/persona.md`
- 코드 수정 추가: `../memo/rules/code-style.md`, `../memo/rules/unity.md`
- 문서 수정 추가: `../memo/rules/docs.md`
- commit과 push 추가: `../memo/rules/commit.md`
- WM 문서 지도: `../memo/wm/README.md`
- 현재 계약: `../memo/wm/apps/`, `../memo/wm/features/`, `../memo/wm/systems/`
- 복잡한 활성 변경: `../memo/changes/README.md`
- 설계 비전: `../memo/wm/design/vision/architecture.md`

위 경로는 자동 로드가 아니다. 작업 전에 에이전트가 직접 읽는다. 모든 수정은 repo lane에서 시작한다. 새 TASK는 만들지 않으며 현재 계약은 App/Feature/System, 복잡한 실행 변경만 Change로 관리한다. lifecycle hook이 없는 클라이언트도 공통 룰과 Git hook 게이트를 따른다.

## 핵심

- Unity New Input System만 사용한다.
- WM C# 스타일은 `CLAUDE.md`의 코딩 스타일을 따른다.
- 작업 중 사용자에게 필요한 Unity 에디터 작업은 해당 Feature/System 또는 활성 Change의 미완료 항목에 즉시 기록한다.
- 새 Unity 파일을 추가하면 `.meta`까지 함께 추적한다.

## Git

이 폴더는 독립 git repo다. 변경은 이 저장소의 세션 lane에서 커밋한다. 폴더명은 `../memo/scripts/lib/repos.mjs`의 `wmDirName()`으로 확인한다.
