# `.claude/scripts/` — Claude session helpers (worktree-local)

Scripts in this directory are intentionally checked into the repo so every
worktree carries the same tooling. They are **not** the canonical home —
`memo/dotfiles/scripts/` is — but several helpers must be available *inside*
the worktree (where Claude Code's cwd lands), so they live here too.

| Script | Purpose | Spec |
| --- | --- | --- |
| `wm-mcp-route.ps1` | Worktree-aware Unity-MCP routing (writes `<worktree>/.mcp.json`). | TASK-WM-109-G |
| `wm-editor-log-tail.ps1` | Editor.log fallback that slices to the latest compile session — surfaces only the *current* errors/warnings, not accumulated history. | TASK-WM-109-D |

## `wm-mcp-route.ps1` — worktree-aware Unity-MCP routing (TASK-WM-109-G)

### Why

Each WitchMendokusai worktree is its own Unity project. When the CoplayDev
`com.coplaydev.unity-mcp` package is active, that Editor spawns its own uvx
HTTP MCP server on a port stored in:

    <worktree>/Library/MCPForUnity/RunState/mcp_http_<port>.pid

Claude Code reads `.mcp.json` from cwd. Without a worktree-specific
`.mcp.json`, a Claude session launched in worktree `B` may end up talking to
the Unity Editor open for worktree `A` (port 8080 collision, EditorPrefs
sharing, etc.). That is precisely issue 7 from TASK-WM-109.

### What this script does

1. Resolves the current worktree root via `git rev-parse --show-toplevel`.
2. Reads `Library/EditorInstance.json` to confirm a Unity Editor is open for
   this worktree and which PID it is.
3. Scans `Library/MCPForUnity/RunState/mcp_http_*.pid` and picks the entry
   whose port is *actually listening* on `127.0.0.1`. Falls back to the most
   recently modified PID file if none are listening.
4. Verifies the uvx server PID inside the file is alive.
5. Writes (or updates) `<worktree>/.mcp.json` so the Claude Code session
   launched from this worktree connects to **this** Editor.

`.mcp.json` is `.gitignore`'d because each worktree binds a different port.

### Usage

```powershell
# Routine: detect port, write .mcp.json
powershell -NoProfile -ExecutionPolicy Bypass -File .claude/scripts/wm-mcp-route.ps1

# Status only (no writes)
powershell -NoProfile -ExecutionPolicy Bypass -File .claude/scripts/wm-mcp-route.ps1 -Status

# Survey every sibling worktree under ../.worktrees
powershell -NoProfile -ExecutionPolicy Bypass -File .claude/scripts/wm-mcp-route.ps1 -All

# Force a specific port (e.g. you set a custom HttpBaseUrl in Window > MCP for Unity)
powershell -NoProfile -ExecutionPolicy Bypass -File .claude/scripts/wm-mcp-route.ps1 -Port 8081

# Dry run -- compute target but do not write
powershell -NoProfile -ExecutionPolicy Bypass -File .claude/scripts/wm-mcp-route.ps1 -DryRun
```

Exit codes:

| Code | Meaning |
| ---: | --- |
| 0 | Wrote (or would have written) a `.mcp.json` with a non-zero port. |
| 2 | No Editor MCP server detected for this worktree; nothing written. |
| other | Unexpected error -- see stderr. |

### Inside Unity

When the Editor is already open, the menu items installed by
`Assets/_WitchMendokusai/Editor/Infra/MCPRouting/McpWorktreeBinder.cs` are
faster than the script and do not require PowerShell:

- `WM > MCP > Bind Claude session to this Editor`
- `WM > MCP > Show Routing Status`
- `WM > MCP > Run wm-mcp-route.ps1 (-Status)` (delegates to this script)

### Tests

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .claude/scripts/wm-mcp-route.tests.ps1
```

The fixture-based test harness simulates a multi-worktree environment (no
Unity required) and verifies six routing cases: `-Status`, `-DryRun`, normal
write, no-Editor failure, cross-worktree isolation, and `-Port` override.

### Diagnostic findings (for future cleanups)

- Per-project HTTP PID file: `Library/MCPForUnity/RunState/mcp_http_<port>.pid`
  -- this is the only authoritative per-worktree signal.
- Per-project TCP bridge port registry:
  `~/.unity-mcp/unity-mcp-port-<sha1[0..8](dataPath)>.json` -- contains
  `unity_port` and `project_path`. Useful for cross-checking which project a
  port belongs to, but it tracks the TCP bridge port, **not** the HTTP MCP
  port the Claude session needs.
- `Library/EditorInstance.json` is the Unity-standard file recording the
  Editor process PID for a given project. Use it to detect "is this
  worktree's Editor currently running?"
- `EditorPrefs.MCPForUnity.HttpBaseUrl` is **per-Unity-install**, not
  per-project -- two Editors share it. Multi-Editor users who want stable
  per-worktree ports should configure each Editor's `HttpBaseUrl` to a
  different value via `Window > MCP for Unity > HTTP URL`.

## `wm-editor-log-tail.ps1` — Editor.log fallback (TASK-WM-109-D)

### Why

`CLAUDE.md § Unity-MCP layer` is explicit that the canonical compile-verify
channel is Unity-MCP `read_console`, because Editor.log is append-only:
errors from past compile attempts pile up in the same file, and a naive
`grep "error CS"` returns *every* error since the Editor was launched.
TASK-WM-056-A (2026-05-10) is the canonical incident — Editor.log grep
reported 259 unique CS errors while the actual current state was 6.

But MCP isn't always available — a worktree Editor may be open *without*
the MCP HTTP server running, the package may be temporarily broken, or a
Claude session may need a quick sanity check before MCP is bound. In those
moments we need an Editor.log fallback that *doesn't* leak history.

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

### Relationship to MCP

This is a **fallback**, not a replacement. The canonical channel remains
Unity-MCP `read_console` (and `McpAutoBinder.cs` now auto-routes the
worktree's `.mcp.json` to keep MCP usable across Editor restarts). Use the
log-tail only when MCP genuinely isn't an option for the current
worktree-session pair.
