# `.claude/scripts/` — Claude session helpers (worktree-local)

Scripts in this directory are intentionally checked into the repo so every
worktree carries the same tooling. They are **not** the canonical home —
`memo/dotfiles/scripts/` is — but several helpers must be available *inside*
the worktree (where Claude Code's cwd lands), so they live here too.

| Script | Purpose | Spec |
| --- | --- | --- |
| `wm-editor-log-tail.ps1` | Editor.log fallback that slices to the latest compile session — surfaces only the *current* errors/warnings, not accumulated history. | TASK-WM-109-D |
| `wm-device-logcat.ps1` | Android `adb logcat` capture — the fallback below the in-app relay (native/IL2CPP crashes, pre-C# startup failures). Canonical phone-log channel is the relay at `/device-log`. | TASK-WM-201 |

## `wm-editor-log-tail.ps1` — Editor.log fallback (TASK-WM-109-D)

### Why

`CLAUDE.md § Unity 통로`의 정본 콘솔 경로는 공식 `unity command console`이다.
Editor.log는 append-only라 이전 컴파일 오류가 섞인다:
errors from past compile attempts pile up in the same file, and a naive
`grep "error CS"` returns *every* error since the Editor was launched.
TASK-WM-056-A (2026-05-10) is the canonical incident — Editor.log grep
reported 259 unique CS errors while the actual current state was 6.

CLI가 도메인 리로드나 메인 스레드 점유로 응답하지 않는 순간에는 현재 세션만 자르는
Editor.log fallback이 필요하다.

### What this script does

1. Reads the entire Editor.log (default:
   `$env:LOCALAPPDATA\Unity\Editor\Editor.log`).
2. Walks backwards to find the **last** occurrence of Unity's canonical
   session boundary marker
   `"Reloading assemblies after forced synchronous recompile"`.
3. Returns only the lines after that marker that match `-Pattern`
   (default: `error CS|warning CS` — per CLAUDE.md, warnings are not
   ignored).
4. Optionally caps output (`-MaxLines`), emits structured JSON
   (`-Json`), or surfaces the marker line itself (`-IncludeMarker`).

### Usage

```powershell
# Default: last compile session, errors + warnings, max 200 lines
powershell -NoProfile -ExecutionPolicy Bypass -File .claude/scripts/wm-editor-log-tail.ps1

# Errors only, last 50
powershell -NoProfile -ExecutionPolicy Bypass -File .claude/scripts/wm-editor-log-tail.ps1 -Pattern 'error CS' -MaxLines 50

# Structured JSON for tooling
powershell -NoProfile -ExecutionPolicy Bypass -File .claude/scripts/wm-editor-log-tail.ps1 -Json
```

Exit codes:

| Code | Meaning |
| ---: | --- |
| 0 | Marker found, scan succeeded (matches may be empty — that means a clean compile). |
| 2 | Editor.log file does not exist at the resolved path. |
| 3 | Editor.log present but no reload marker found — log is from a session that never reloaded assemblies; results are unreliable and an empty set is emitted. |

### Tests

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .claude/scripts/wm-editor-log-tail.tests.ps1
```

Seven fixture-based cases (no Unity required) verify the
session-isolation behaviour against a synthetic two-session log, plus the
JSON shape, max-lines cap, pattern override, marker inclusion, no-marker
exit code, and missing-file exit code.

### Unity CLI와의 관계

이 스크립트는 fallback이다. 정본은 공식 `unity command console`이며, CLI가 60초 안에
서비스 가능 상태로 돌아오지 않을 때만 현재 Editor.log 세션을 확인한다.
