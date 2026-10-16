# Native content authoring

Open **WM > Authoring > Scene workspace** in Unity. This early authoring tool edits native scene documents in an isolated preview scene and uses the same data and motion core as runtime playback.

For a first session, choose **Open greeting example**. Two block figures appear in the character view with a two-second waving motion. Press **Play**, or move **Time** to inspect the arm poses. The example needs no external model files. Use **Export sequence** to keep a separate copy with its resources, then **Open sequence** to resume it. This example teaches scene and motion controls; it is not a detailed character model.

The workspace language selector offers Korean and English without changing saved identifiers. **Show character view** brings the preview back. Light and camera icons start hidden in this preview; enable **Show scene icons** when you need them. Other scene views retain their own display settings.

1. **Open scene** selects a native scene JSON file. Keep its resource files with it. The shared library can import a scene and store identical resource bytes once across multiple variants.
2. Find a named control and edit its local position, rotation or scale. Unity selection also follows camera or mesh selections back to the document node. Stable IDs remain independent of visible GameObject names.
3. Expand **Limb goals** and load rig bindings when needed. Move a goal and bend pole, then apply it. **Record limb key** records both controlled joints at the current motion time. The first limb solver supports rigid chains with positive uniform world scale.
4. Expand **Expressions** to choose a mesh and search its morph controls. **Key** records that value. Imported low-level morph IDs may still need semantic labels.
5. In **Motion clip**, set a duration, move the time slider, pose a node and choose **Record node key**. Record another key at another time. Playback interpolates poses and numerical properties. Save the motion clip separately from the current scene snapshot.
6. **Camera from view** adds a camera using the stage viewpoint. Select its node to record camera movement, or use **Record lens key** for lens changes. Add a light and record intensity or color keys. **Use lit surfaces** switches the basic surface model to use scene lighting.
7. **Add actor** imports another native scene as a separate actor. Its node identities, rig roles and resource references are separated from existing actors. The existing motion draft is preserved. Imported actor cameras and lights are excluded so the current shot remains in control.
8. **Save as** writes the current scene snapshot and verified resources. Rig sidecars are preserved. A scene snapshot stores the current pose; a motion clip stores changes over time.
9. **Export sequence** saves the scene and current motion draft together as a `.sequence.json` document with shared resource files. **Open sequence** restores both. Move the entire export folder when transferring it. Optional rig sidecars preserve editor limb controls.

Working scene and motion drafts are checkpointed every ten seconds when changed, and before closing or script reload. Recovery is stored in the local project Library and references existing resources. It does not replace source files or duplicate model files. Keep source resources available until the scene has been saved or imported into the shared library.

Expand **Tracks and keys** to remove a key at the current time or remove a whole track. **Undo key edit** and **Redo key edit** restore up to 100 draft edits. This history is separate from Unity transform editing.

Click a key time or use **Previous key** / **Next key** for exact navigation. The first eight keys of each track are shown; navigation visits all keys. Scrubbing can reach the final key of a looping clip; preview playback still loops normally.

Expand **Surfaces** to choose a material recipe, change its color, lighting, alpha mode, roughness, metallic value, emission or texture coordinates, then choose **Apply surface**. The slot count shows how widely the recipe is shared; **Select surface users** selects the meshes that use it. Unapplied values are temporary and Reset discards them. Existing textures remain referenced by resource identity.

**Undo scene change** / **Redo scene change** restore camera, light and surface changes through document snapshots, including the pose at the change. Scene and key histories use the same engine-independent history module, with 100 total states and a shared character budget per history. Opening a scene or adding an actor starts a new scene history. A change that would remove a motion target is rejected while its keys remain; remove those keys or start a new clip first.

Expand **Body and face controls** to edit named controls included with a scene. Choose **Apply body controls** to update the preview or **Reset body controls** to apply their calibrated defaults. To create a control, select a part in Pose controls, expand **Create scale control**, enter a name and range, and choose overall size, width, height or depth. The selected part's current scale becomes the reference at value 1. The range must be positive and contain 1. A scale component already owned by another control cannot be assigned again.

Applied values and their calibration are stored in scene snapshots, scene Undo/Redo, recovery and exported sequences. Applying a control recalculates from its reference, so repeated changes do not accumulate scale error. It is an explicit pose edit: absolute motion keys can later overwrite the same components. Detailed anatomical correction and motion retargeting remain incomplete.

For a scene that already has motion, set the pending control values and choose **Export controls with fitted motion** before applying them. This exports a separate sequence with the scene and motion keys adjusted together. Scale uses the ratio between old and new calibrated values; position and morph channels use their difference. Timing, rotation and unrelated channels stay unchanged. The current workspace and its edit histories are preserved. Choose **Open sequence** to inspect and play the exported variant. Morph changes that would push any key below 0 or above 1 are rejected. This fits controls on the same rig; transferring motion between different skeletons remains unfinished.

The same engine-independent operation is available through `SceneSequence.SequenceParameters.WithValues(sequence, values, morphTarget)`. A host resolves morph binding indices to its motion property IDs when animated morphs change. It returns an independent ordinary sequence and validates the whole result before returning it. A posed snapshot retains its animation variation rather than resetting to the profile baseline. Both editor and runtime consume the result through their existing sequence paths.

The reusable packages are `Appearance`, `Geometry`, `Scene Content`, `Motion`, `Rigging`, `Scene Sequence` and `Snapshot History`. Their cores have no engine or game dependencies. Unity adapters own generated assets and bind transforms, rendering and playback. `WM.ContentRuntime` composes scene data with motion property ports; `WM.ContentAuthoring.Editor` provides the authoring host.

At runtime, add `ContentScenePlayer` to a host GameObject and initialize it with a scene document, an `IContentSource`, an `ISurfaceFactory` and motion clips. The content stays inactive until construction and clip binding succeed. Call `Play`, `Pause`, `Seek` or `Unload`. Clips that write the same pose or numerical property are rejected; deliberate blending requires a separate mixer. The host owns the scene context, clock policy and camera/UI integration.

Alternatively, read an exported sequence with `SequenceFiles.Read(path)`, then pass it to `ContentScenePlayer.Initialize(sequence, source, surfaces)`. Supply a `DirectoryContentSource` rooted at the export directory. The sequence supports multiple non-overlapping clips; this editor currently edits one combined motion draft. Loading a multi-clip sequence into this editor is rejected explicitly. Event/audio scheduling and clip layering are separate unfinished capabilities.

[Portable core example](../Portable/ContentCore/README.md) compiles and runs the common core sources outside Unity.

[Module boundaries](ContentArchitecture.md) describes data ownership, host ports and adapter composition.

This release covers scene snapshots, pose/value clips, basic surfaces, punctual lights, cameras and two-segment limb goals. Secondary dynamics, persistent constraints, semantic body customization, events, audio, external motion import and final game UI integration remain incomplete. Use the dedicated scene buttons for structural changes. Unity transform Undo references from before a preview rebuild do not survive it. The native document and recovery data remain independent of that preview lifecycle.
