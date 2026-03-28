---
name: commit
description: >-
  Stages and commits only files changed in the current chat session, with a
  short message styled like recent repo history. Use when the user says /commit,
  asks to commit session changes only, or to commit what was edited this session.
---

# /commit (세션 변경만 커밋)

## 목표

이 대화에서 에이전트가 **실제로 수정한 파일**만 골라 스테이징하고 커밋한다. 커밋 제목·본문은 **짧게**, `git log` 최근 몇 개 톤에 맞춘다.

## 절차

1. **세션 변경 목록 확정**  
   이번 대화에서 도구로 편집·추가·삭제한 경로만 후보로 쓴다. (읽기만 한 파일은 제외.)

2. **저장소 상태 확인**  
   `git status`, 필요 시 `git diff`로 워킹트리를 본다.

3. **겹침 검사 (필수)**  
   후보 파일이 `git status`에 **수정됨(M)** 등으로 잡혀 있을 때:
   - 이 세션에서만 건드린 파일 → 그대로 진행.
   - **같은 파일에** 이 세션 바깥 변경(다른 에이전트·수동 편집)이 **같이 섞여** 있을 가능성이 있으면 **커밋하지 말고** 사용자에게 먼저 묻는다.

   **질문 예시 (한 번에 제시):**
   - `path/to/File.cs`에 이번 세션 변경 외에 다른 변경이 같이 있습니다. 어떻게 할까요?
     - **A)** 이 파일은 이번 커밋에서 빼기  
     - **B)** 전부 함께 커밋하기  
     - **C)** 중단 (직접 정리 후 다시)

   판단이 애매하면 `git diff <file>`로 범위를 보고, 여전히 세션만 분리하기 어렵면 **반드시 C 또는 사용자 지시**를 받는다.

4. **이전 커밋 스타일**  
   `git log -5 --oneline` (또는 `--format=...`)로 제목 패턴(접두어, 영/한, 길이)을 보고 **같은 스타일**로 짧게 쓴다.

5. **스테이징·커밋**  
   - `git add -- <세션 파일들만>`  
   - `git commit -m "제목"` — 본문이 필요하면 `-m "제목" -m "한두 문장"` 정도만.

6. **스테이징에 다른 파일이 섞이지 않게** 한다. `git add -A` / `git add .` 로 세션 무관 파일을 넣지 않는다.

## 하지 말 것

- 세션에서 수정하지 않은 파일을 임의로 포함하지 않는다.
- 겹침이 있는데 사용자 답 없이 mixed 파일을 커밋하지 않는다.
- 불필요하게 긴 본문·장황한 설명을 쓰지 않는다.

## 트리거

사용자가 **`/commit`** 이라고 하거나, 동일한 의미(이번에 고친 것만 커밋)로 요청할 때 이 스킬을 따른다.
