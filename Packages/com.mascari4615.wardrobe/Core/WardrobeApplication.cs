using System;
using System.Collections.Generic;
using Rigging;
using SceneContent;

namespace Wardrobe
{
    public static class WardrobeApplication
    {
        // Dressing first takes off everything this wardrobe put on, then wears the outfit. Other edits to the scene are kept,
        // so switching outfits never accumulates parts and never needs the original actor.
        public static SceneDocument Dress(SceneDocument current, RigDefinition rig, WardrobeDocument wardrobe, string outfitId)
        {
            if (current == null || rig == null || wardrobe == null) throw new ArgumentNullException("scene, rig and wardrobe are required");
            rig.Validate();
            wardrobe.Validate();
            Outfit outfit = Array.Find(wardrobe.Outfits, candidate => candidate.Id == outfitId);
            if (outfit == null) throw new ArgumentException("Unknown outfit: " + outfitId);
            List<Worn> worn = Resolve(wardrobe, outfit);
            HashSet<string> roles = new HashSet<string>(StringComparer.Ordinal);
            foreach (RigJoint joint in rig.Joints) roles.Add(joint.Role);
            foreach (Worn item in worn)
            {
                if (roles.Contains(item.Part.Anchor) == false) throw new ArgumentException("Rig has no anchor role " + item.Part.Anchor + " for part " + item.Part.Id);
                foreach (PartBinding binding in item.Part.Bindings)
                    if (roles.Contains(binding.Role) == false) throw new ArgumentException("Rig has no role " + binding.Role + " for part " + item.Part.Id);
            }
            SceneDocument result = Undress(current, wardrobe);
            HashSet<string> hiddenNodes = new HashSet<string>(StringComparer.Ordinal);
            foreach (Worn item in worn)
                foreach (string regionId in item.Part.HiddenRegions)
                    foreach (BodyRegion region in wardrobe.Regions)
                        if (region.Id == regionId) hiddenNodes.UnionWith(region.MeshNodes);
            foreach (MeshInstance mesh in result.Meshes)
                if (hiddenNodes.Contains(result.Nodes[mesh.Node].Id)) mesh.Hidden = true;
            foreach (Worn item in worn)
            {
                SceneDocument piece = SceneCloning.Copy(item.Part.Scene);
                if (item.Variant != null)
                    foreach (MaterialOverride change in item.Variant.Materials) piece.Materials[change.Material].BaseColor = (float[])change.BaseColor.Clone();
                Dictionary<string, string> shared = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (PartBinding binding in item.Part.Bindings) shared.Add(binding.PartNode, rig.Resolve(binding.Role));
                result = SceneComposition.Attach(result, piece, item.Part.Id, rig.Resolve(item.Part.Anchor), shared);
            }
            return result;
        }

        // Removes every part this wardrobe could have attached (nodes, meshes, materials and resources under the part's namespace)
        // and shows the body regions again. The input is not modified.
        public static SceneDocument Undress(SceneDocument current, WardrobeDocument wardrobe)
        {
            if (current == null || wardrobe == null) throw new ArgumentNullException("scene and wardrobe are required");
            wardrobe.Validate();
            SceneDocument result = SceneCloning.Copy(current);
            List<string> prefixes = new List<string>();
            foreach (WardrobePart part in wardrobe.Parts) prefixes.Add(part.Id + ":");
            bool[] removedNode = new bool[result.Nodes.Length];
            bool any = false;
            for (int index = 0; index < removedNode.Length; index++)
            {
                removedNode[index] = HasPrefix(result.Nodes[index].Id, prefixes);
                any |= removedNode[index];
            }
            HashSet<string> shown = new HashSet<string>(StringComparer.Ordinal);
            foreach (BodyRegion region in wardrobe.Regions) shown.UnionWith(region.MeshNodes);
            foreach (MeshInstance mesh in result.Meshes)
                if (shown.Contains(result.Nodes[mesh.Node].Id)) mesh.Hidden = false;
            if (any == false) return result;

            int[] nodeMap = Remap(removedNode);
            for (int index = 0; index < result.Nodes.Length; index++)
            {
                if (removedNode[index]) continue;
                int parent = result.Nodes[index].Parent;
                if (parent >= 0 && removedNode[parent]) throw new ArgumentException("Node " + result.Nodes[index].Id + " hangs from a removed part");
            }
            bool[] removedMesh = new bool[result.Meshes.Length];
            for (int index = 0; index < removedMesh.Length; index++) removedMesh[index] = removedNode[result.Meshes[index].Node];
            int[] meshMap = Remap(removedMesh);
            bool[] keptMaterial = new bool[result.Materials.Length];
            bool[] usedByRemoved = new bool[result.Materials.Length];
            for (int index = 0; index < result.Meshes.Length; index++)
                foreach (int material in result.Meshes[index].Materials)
                {
                    if (removedMesh[index]) usedByRemoved[material] = true;
                    else keptMaterial[material] = true;
                }
            bool[] removedMaterial = new bool[result.Materials.Length];
            for (int index = 0; index < removedMaterial.Length; index++) removedMaterial[index] = usedByRemoved[index] && keptMaterial[index] == false;
            int[] materialMap = Remap(removedMaterial);

            List<SceneNode> nodes = new List<SceneNode>();
            for (int index = 0; index < result.Nodes.Length; index++)
            {
                if (removedNode[index]) continue;
                SceneNode node = result.Nodes[index];
                node.Parent = node.Parent < 0 ? -1 : nodeMap[node.Parent];
                nodes.Add(node);
            }
            List<MeshInstance> meshes = new List<MeshInstance>();
            for (int index = 0; index < result.Meshes.Length; index++)
            {
                if (removedMesh[index]) continue;
                MeshInstance mesh = result.Meshes[index];
                mesh.Node = nodeMap[mesh.Node];
                if (mesh.RootBone >= 0) mesh.RootBone = nodeMap[mesh.RootBone];
                for (int bone = 0; bone < mesh.Bones.Length; bone++) mesh.Bones[bone] = nodeMap[mesh.Bones[bone]];
                for (int material = 0; material < mesh.Materials.Length; material++) mesh.Materials[material] = materialMap[mesh.Materials[material]];
                meshes.Add(mesh);
            }
            List<SurfaceMaterial> materials = new List<SurfaceMaterial>();
            for (int index = 0; index < result.Materials.Length; index++) if (removedMaterial[index] == false) materials.Add(result.Materials[index]);
            List<ContentResource> resources = new List<ContentResource>();
            foreach (ContentResource resource in result.Resources) if (HasPrefix(resource.Id, prefixes) == false) resources.Add(resource);
            if (result.Parameters != null)
            {
                foreach (SceneParameterBinding binding in result.Parameters.Bindings)
                {
                    if (binding.Mesh < 0) continue;
                    if (meshMap[binding.Mesh] < 0) throw new ArgumentException("A removed part carries parameter binding " + binding.Property);
                    binding.Mesh = meshMap[binding.Mesh];
                }
            }
            foreach (SceneCamera camera in result.Cameras)
            {
                if (removedNode[camera.Node]) throw new ArgumentException("A camera hangs from a removed part");
                camera.Node = nodeMap[camera.Node];
            }
            foreach (SceneLight light in result.Lights)
            {
                if (removedNode[light.Node]) throw new ArgumentException("A light hangs from a removed part");
                light.Node = nodeMap[light.Node];
            }
            result.Nodes = nodes.ToArray();
            result.Meshes = meshes.ToArray();
            result.Materials = materials.ToArray();
            result.Resources = resources.ToArray();
            SceneValidation.Ensure(result);
            return result;
        }

        private static bool HasPrefix(string id, List<string> prefixes)
        {
            foreach (string prefix in prefixes) if (id.StartsWith(prefix, StringComparison.Ordinal)) return true;
            return false;
        }

        private static int[] Remap(bool[] removed)
        {
            int[] map = new int[removed.Length];
            int next = 0;
            for (int index = 0; index < removed.Length; index++) map[index] = removed[index] ? -1 : next++;
            return map;
        }

        private static List<Worn> Resolve(WardrobeDocument wardrobe, Outfit outfit)
        {
            Dictionary<string, WardrobeSlot> slots = new Dictionary<string, WardrobeSlot>(StringComparer.Ordinal);
            foreach (WardrobeSlot slot in wardrobe.Slots) slots.Add(slot.Id, slot);
            List<Worn> worn = new List<Worn>();
            HashSet<string> picked = new HashSet<string>(StringComparer.Ordinal);
            foreach (OutfitPick pick in outfit.Picks)
            {
                if (pick == null || slots.ContainsKey(pick.Slot) == false || picked.Add(pick.Slot) == false)
                    throw new ArgumentException("Outfit picks must name each existing slot at most once");
                WardrobePart part = wardrobe.FindPart(pick.Part);
                if (part.Slot != pick.Slot) throw new ArgumentException("Part " + part.Id + " does not belong to slot " + pick.Slot);
                PartVariant variant = null;
                if (string.IsNullOrEmpty(pick.Variant) == false)
                {
                    variant = Array.Find(part.Variants, candidate => candidate.Id == pick.Variant);
                    if (variant == null) throw new ArgumentException("Unknown variant " + pick.Variant + " of part " + part.Id);
                }
                worn.Add(new Worn { Slot = slots[pick.Slot], Part = part, Variant = variant });
            }
            foreach (Worn item in worn)
                foreach (string other in item.Slot.Conflicts)
                    if (picked.Contains(other)) throw new ArgumentException("Slots conflict: " + item.Slot.Id + " and " + other);
            worn.Sort((left, right) =>
            {
                int layer = left.Slot.Layer.CompareTo(right.Slot.Layer);
                return layer != 0 ? layer : string.CompareOrdinal(left.Slot.Id, right.Slot.Id);
            });
            return worn;
        }

        private sealed class Worn
        {
            public WardrobeSlot Slot;
            public WardrobePart Part;
            public PartVariant Variant;
        }
    }
}
