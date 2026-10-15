# Scene Content

Portable scene composition: a validated node graph, local poses, mesh instances, skin roots and joints, morph weights, surface recipes and content-addressed resources. Nodes have persistent IDs independent of editor hierarchy indices. Core has no engine, editor, storage or game references.

Adapters are separate assemblies: strict JSON serialization, a directory resource source with path containment and checksum verification, and Unity scene construction. Callers supply `IContentSource` and a shader. The initial surface adapter expects URP-compatible base color/base map properties. It is a basic surface adapter, not a universal shader translator.

The Unity loader stages a scene inactive, validates geometry bindings, shares repeated mesh/texture resources and owns generated assets for cleanup. Failed loads dispose partially allocated resources. `SceneInstance.FindNode` resolves stable IDs for host control and survives editor reloads.

Skinning fidelity is a host rendering policy. The loader rejects a host influence limit below the geometry requirement, unless reduced fidelity is explicitly requested. Set an appropriate host skin-weight quality before loading; per-renderer quality alone does not guarantee full influence evaluation. The module does not silently change project quality settings.

The contract describes scene snapshots with editable transforms and morphs, perspective/orthographic cameras, punctual lights and basic surface recipes. Behavior controllers, simulation, animation clips and material graphs are separate capabilities; no opaque compatibility payload is stored here.

Optional `Parameters` embeds an Appearance parameter profile with explicit bindings to node scale, node position or mesh morph components. `SceneParameters.WithValues` returns an edited copy; `SetValues` validates a complete candidate before changing its endpoints. Each property binds exactly once and competing bindings are rejected. `SceneParameterAuthoring.AddScaleControl` creates a named control calibrated to a node's current scale. Cloning, composition and pruning preserve these controls and their stable targets. Existing documents may omit the profile.

Controls are explicit editing actions. Loading preserves the current scene snapshot without recomputing its pose. An absolute motion track may subsequently overwrite an endpoint; persistent constraints and motion retargeting require separate composition policies.

`SceneSnapshot.Capture` copies current local poses and expression weights without changing the source document. `SceneFiles.Save` verifies every resource, stages new blobs and atomically replaces the manifest after all resources are present. Existing corrupt resources cause a failure before the saved manifest changes. Saving to another folder produces a self-contained native scene without private provenance data. Optional node labels help authoring while persistent IDs remain the binding contract.

The Unity adapter normalizes rotations for execution and retains their original document values. Snapshots preserve an unedited rotation exactly, including across repeated saves, while recording even small deliberate edits. Resetting a node to its loaded rotation recovers the original document value; comparison uses exact runtime components rather than an angular tolerance.

Camera data includes projection, vertical field of view, orthographic half-height, near/far clips, clear color and authored activation. Hosts can suppress camera activation when loading an editor preview; snapshots preserve the authored activation policy. Lights use explicit relative intensity and support directional, point and spot types. Physical-unit calibration and environment lighting remain separate host policies.

`LoadWithSurfaces` takes an `ISurfaceFactory`. `SingleShaderSurfaceFactory` forces a caller-selected shader; `StandardSurfaceFactory` selects caller-provided unlit or lit shaders using each surface's lighting model. Roughness maps to one minus smoothness, with metallic and linear emission values. Generated materials belong to the scene instance. A custom factory must return a new owned material for each request, not borrowed shared assets.

`ScenePruning.Snapshot` is an explicit optimization operation. It retains mesh nodes, skin bones, roots, camera/light nodes, caller-pinned nodes and all ancestors, then remaps indices while preserving stable IDs. It never runs implicitly during loading. `SceneCloning.Copy` gives an independent editable copy, including nested mutable arrays.

`ContentLibrary.Import` rewrites resource paths into a shared hash store and publishes a content-addressed scene manifest after verifying every blob. Pose, material and camera variants share identical resource bytes. Imported scenes remain ordinary scene documents, loadable through `DirectoryContentSource` and exportable through `SceneFiles.Save`. Reads and writes reject escaping paths and linked ancestors. Rig or motion composition is a host responsibility; the WM authoring host preserves rig sidecars.
