---
name: commit-session
description: Commit only files touched in this session, grouped by topic into separate commits. Never pushes.
---

Commit only the files you touched in this session, grouped by topic into separate commits. Never push.

Steps:
1. Review this conversation and identify every file you Read, Edited, or Wrote using tools.
2. Run `git status` and `git diff` to see current changes. Cross-reference with step 1 — only include files that appear in both (touched in session AND dirty in git).
3. Group those files by topic (e.g. UI, gameplay, config, assets). Prefer fewer coherent commits over many tiny ones.
4. For each group, stage only those files and commit with a concise message describing what changed and why.
5. After all commits, run `git status` to confirm.

Rules:
- NEVER run `git push` or any remote operation.
- NEVER use `git add .` or `git add -A` — always add specific files by name.
- NEVER amend existing commits — always create new ones.
- Pass commit messages via heredoc to preserve formatting.
- Append to every commit message: `Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>`
- If no session-touched files are dirty, report that and stop.
