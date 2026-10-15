# Scene Content

Portable scene composition: a validated node graph, local poses, mesh instances, skin roots and joints, morph weights, surface recipes and content-addressed resources. Nodes have persistent IDs independent of editor hierarchy indices. Core has no engine, editor, storage or game references.

Adapters are separate assemblies: strict JSON serialization, a directory resource source with path containment and checksum verification, and Unity scene construction. Callers supply `IContentSource` and a shader. The initial surface adapter expects URP-compatible base color/base map properties. It is a basic surface adapter, not a universal shader translator.

The Unity loader stages a scene inactive, validates geometry bindings, shares repeated mesh/texture resources and owns generated assets for cleanup. Failed loads dispose partially allocated resources. `SceneInstance.FindNode` resolves stable IDs for host control and survives editor reloads.

Skinning fidelity is a host rendering policy. The loader rejects a host influence limit below the geometry requirement, unless reduced fidelity is explicitly requested. Set an appropriate host skin-weight quality before loading; per-renderer quality alone does not guarantee full influence evaluation. The module does not silently change project quality settings.

This initial contract describes a scene snapshot with editable transforms and morphs. Behavior controllers, simulation, animation clips, camera/lights and material graphs are separate capabilities; no opaque compatibility payload is stored here.
