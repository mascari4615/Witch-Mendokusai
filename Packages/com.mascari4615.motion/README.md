# Motion

Reusable pose animation with a pure .NET core. No WM, editor, scene-document, avatar or source-format dependency.

`Core` defines local pose values, an `IPoseTarget` port, a versioned `PoseClip`, key recording and deterministic sampling. `Serialization` validates JSON. `Unity` adapts a caller-supplied transform resolver and provides `PoseClipPlayer`. The host owns node IDs, scheduling, assets, binding and lifetime.

Coordinates use right-handed Y-up, Z-back, matching Scene Content. Time is seconds, quaternions are normalized XYZW, and tracks contain position, rotation and scale. Position and scale interpolate linearly; rotations use shortest-path spherical interpolation. Nonlooping time clamps; looping time wraps, including negative time. A looping sample at the duration returns the start. Keys outside the duration, duplicate times or targets, nonfinite components and invalid rotations fail validation.

The sampler snapshots input. Editing a clip after constructing a sampler does not change its output. Applying a frame resolves every target before writing any pose, so missing nodes cannot leave half a frame applied. Host writes should be synchronous and nonthrowing after resolution.

Initialize `PoseClipPlayer` with a clip and a `TransformPoseTarget`, call `Play`, `Pause` or `Seek`. The player advances using scaled Unity game time and stops at the end of a nonlooping clip. Hosts can drive `PoseClipSampler` directly for a different clock, sequencer, network synchronization or offline evaluation.

This first version handles local transforms. Camera optical properties, expression channels, constraints, events and audio are separate pending capabilities; they are not encoded as opaque payloads.
