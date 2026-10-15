# Rigging

Engine-free role bindings and analytic two-segment limb solving. The core knows no character names, engine hierarchy, game, animation controller or source format. Hosts provide joint positions and world rotations, a goal and a bend pole.

`RigDefinition` maps caller-defined roles to persistent node IDs and groups three roles into named limbs. The geometry solver also works without a rig definition. It preserves segment lengths, clamps unreachable goals to the reach interval and reports whether the requested goal is reachable. Collinear poles use the original bend direction, then a deterministic perpendicular fallback. Rotations use right-handed Y-up, Z-back coordinates.

`TransformLimbIk` is an optional Unity adapter. It validates ancestry and positive uniform world scale before applying world rotations. Nonuniform scale, reflections, zero-length segments and unsupported intermediate transforms fail explicitly. If a hierarchy fails the resulting endpoint check, the adapter restores both original rotations. It does not stretch bones or silently move the root. This first adapter is for rigid limb chains; deformable rigs require a separate scale-aware adapter.
