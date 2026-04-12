---
name: commit-all
description: Commit all dirty files (including those not touched this session), grouped by topic into separate commits. Never pushes.
---

Commit all dirty files (staged and unstaged), grouped by topic into separate commits. Never push.

Steps:
1. Run `git status` to see all modified, deleted, and untracked files.
2. Run `git diff` (and `git diff --cached` for staged changes) to understand what changed.
3. Group all dirty files by topic (e.g. UI, gameplay, config, assets, scene, scripts). Prefer fewer coherent commits over many tiny ones. Files that belong together logically should be in the same commit even if they span directories.
4. For each group, stage only those files and commit with a concise message describing what changed and why.
5. After all commits, run `git status` to confirm everything is clean.

Rules:
- NEVER run `git push` or any remote operation.
- NEVER use `git add .` or `git add -A` — always add specific files by name.
- NEVER amend existing commits — always create new ones.
- Pass commit messages via heredoc to preserve formatting.
- Append to every commit message: `Co-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>`
- If there is nothing to commit, report that and stop.
- If a file looks like it may contain secrets (.env, credentials, keys), warn the user and skip it.
