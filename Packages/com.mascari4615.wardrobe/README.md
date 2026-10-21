# Wardrobe

Engine-free outfit composition for a rigged scene. The core knows no character names, game, engine hierarchy or source format.

- `WardrobeDocument`: slots (layer order, conflicts), body regions (named sets of mesh nodes), wearable parts and outfits.
- `WardrobePart`: a self-contained scene document, one slot, an anchor role, optional bone bindings and material variants. Bone bindings redirect the part's own nodes to the host's nodes by rig role, so skinned pieces deform with the host skeleton. A part may hide body regions. An optional carrier role names an ancestor of the anchor that the part follows instead, so a necklace placed at the neck can ride the chest and stay clear of the body when the neck bends. The offset is computed from the host's rest pose when dressing.
- `WardrobeApplication.Dress(actor, rig, wardrobe, outfitId)`: validates the outfit (one pick per slot, part belongs to slot, no conflicting slots, rig roles exist), then rebuilds the dressed scene from the unchanged actor. Switching outfits never accumulates edits.
- `SceneComposition.Attach` (Scene Content) is the underlying merge: namespaced nodes, meshes, materials and resources are added under an existing node.

Parts may contain nodes, meshes, materials, resources, springs and colliders. Springs and colliders are namespaced with the part and removed again on undress; a spring or collider on a shared host node is rejected. Cameras, lights, parameters and limb constraints are rejected for now. A hidden body mesh that carries a parameter binding is rejected rather than silently dropped.
