---
name: commit-all
description: >-
  Proposes sequential git commits until the working tree is clean, following
  the project commit skill for grouping and message style. Use when the user
  says /commit-all or asks to commit everything in steps. Each commit requires
  explicit user approval before running git commit.
---

# /commit-all (남은 변경을 나눠 커밋)

## 관계

[commit 스킬](../commit/SKILL.md)의 **메시지 톤·짧은 제목·`git add` 범위·겹침 처리 원칙**을 따른다. 차이점은 **범위**: 여기서는 워킹트리에 남은 변경을 **논리적 묶음**으로 나누어, **빌 때까지** 반복한다.

## 절대 규칙 (위반 금지)

- **`git commit`을 사용자 승인 없이 실행하지 않는다.**
- 각 묶음마다 **반드시** 다음을 먼저 제시하고 **응답을 기다린다**:
  - 포함할 파일 목록(또는 경로)
  - 제안 커밋 메시지(및 필요 시 한두 줄 본문)
  - [commit 스킬](../commit/SKILL.md)에 해당하면 겹침·불확실성 질문
- 사용자가 그 **해당 커밋만** 진행한다고 명시하기 전에는 `git add`/`git commit`을 하지 않는다. (승인 후에만 실행.)

## 절차

1. `git status`(및 필요 시 `git diff --stat`)로 **남은 변경**을 확인한다.
2. 변경이 없으면 종료한다.
3. **한 번에 하나의 커밋 후보만** 고른다 (주제별로 파일 묶기: 예 카메라, 입력, 에셋 등).
4. 위 **절대 규칙**에 따라 사용자에게 질문한다.
5. 사용자가 진행을 허락하면: `git add -- <해당 파일들만>` 후 `git commit` (commit 스킬과 같이 `git add -A`/`.` 남용 금지).
6. 다시 1로 돌아가 **남은 것이 없을 때까지** 반복한다.

## 트리거

사용자가 **`/commit-all`** 이라고 하거나, 남은 변경을 **여러 커밋으로 나눠** 정리해 달라고 할 때 이 스킬을 따른다.

## 하지 말 것

- 승인 전 커밋.
- 한 턴에 여러 커밋을 **연속으로** 찍기 (매 커밋마다 질문·승인).
- 사용자가 세션 전용만 원하는 경우: 그때는 [commit 스킬](../commit/SKILL.md)이 맞고, commit-all은 **전체 워킹트리** 기준이므로 의도를 확인한다.
