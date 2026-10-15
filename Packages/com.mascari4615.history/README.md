# Snapshot History

`SnapshotHistory<T>` stores serialized states supplied by the host. It has no scene, motion, JSON, file-system, game or engine dependency. WM uses the same class for scene structural edits and motion key edits; the portable example uses a different JSON codec.

Pass `Func<T, string>` and `Func<string, T>` to construct it. `Record(previous)` captures immutable serialized data before a simple change. `Execute(previous, change)` records only after the change callback succeeds. `Undo(current, apply)` and `Redo(current, apply)` decode a candidate and commit history movement after the optional application callback succeeds.

The host must stage changes before replacing live state. History cannot roll back arbitrary callback side effects. Failed serialization or staged application leaves its stacks unchanged. Starting a new edit clears redo. Call `Clear` when changing the document/resource context.

Defaults allow at most 100 total states and 32 million UTF-16 characters across both stacks. The oldest distant states are evicted as needed. A single snapshot larger than the configured character budget is rejected before executing a change. Resource bytes should remain outside snapshots.
