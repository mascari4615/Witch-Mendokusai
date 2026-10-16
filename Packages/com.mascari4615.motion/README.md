# Motion

Reusable pose animation with a pure .NET core. No WM, editor, scene-document, avatar or source-format dependency.

`Core` defines local pose values, an `IPoseTarget` port, a versioned `PoseClip`, key recording and deterministic sampling. `Serialization` validates JSON. `Unity` adapts a caller-supplied transform resolver and provides `PoseClipPlayer`. The host owns node IDs, scheduling, assets, binding and lifetime.

Coordinates use right-handed Y-up, Z-back, matching Scene Content. Time is seconds, quaternions are normalized XYZW, and tracks contain position, rotation and scale. Position and scale interpolate linearly; rotations use shortest-path spherical interpolation. Nonlooping time clamps; looping time wraps, including negative time. A looping sample at the duration returns the start. Keys outside the duration, duplicate times or targets, nonfinite components and invalid rotations fail validation.

The sampler snapshots input. Editing a clip after constructing a sampler does not change its output. Applying a frame resolves every target before writing any pose, so missing nodes cannot leave half a frame applied. Host writes should be synchronous and nonthrowing after resolution.

Initialize `PoseClipPlayer` with a clip and a `TransformPoseTarget`, call `Play`, `Pause` or `Seek`. The player advances using scaled Unity game time by default and stops at the end of a nonlooping clip. A host can explicitly pass `useUnscaledTime: true` when initializing playback that must continue while scaled game time is paused. Hosts can drive `PoseClipSampler` directly for another clock, sequencer, network synchronization or offline evaluation.

Optional scalar tracks animate named numerical properties through `IValueTarget`. Bindings validate both availability and semantic bounds before any pose or property in a frame is written. The engine-free `FloatPropertyTarget` adapts host read/write delegates. The WM host binds expression weights, lens values, light intensity and color; the core knows none of these concepts. Older transform-only files remain readable.

Constraints, events, audio and external motion import remain separate pending capabilities. Property identities are host contracts and must remain stable when reusing clips. A host may use a different binding map for another compatible rig or scene.
