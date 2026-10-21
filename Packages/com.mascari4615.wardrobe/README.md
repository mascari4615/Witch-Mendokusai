# Wardrobe

Engine-free outfit composition for a rigged scene. The core knows no character names, game, engine hierarchy or source format.

- `WardrobeDocument`: slots (layer order, conflicts), body regions (named sets of mesh nodes), wearable parts and outfits.
- `WardrobePart`: a self-contained scene document, one slot, an anchor role, optional bone bindings and material variants. Bone bindings redirect the part's own nodes to the host's nodes by rig role, so skinned pieces deform with the host skeleton. A part may hide body regions.
- `WardrobeApplication.Dress(actor, rig, wardrobe, outfitId)`: validates the outfit (one pick per slot, part belongs to slot, no conflicting slots, rig roles exist), then rebuilds the dressed scene from the unchanged actor. Switching outfits never accumulates edits.
- `SceneComposition.Attach` (Scene Content) is the underlying merge: namespaced nodes, meshes, materials and resources are added under an existing node.

Parts may contain only nodes, meshes, materials and resources. Cameras, lights, parameters, limb constraints, springs and colliders are rejected for now. A hidden body mesh that carries a parameter binding is rejected rather than silently dropped.
