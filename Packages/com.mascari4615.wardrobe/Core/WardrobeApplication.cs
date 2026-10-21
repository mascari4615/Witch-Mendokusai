using System;
using System.Collections.Generic;
using Rigging;
using SceneContent;

namespace Wardrobe
{
    public static class WardrobeApplication
    {
        // The actor document is never modified. Each call rebuilds the dressed scene from the actor, so switching outfits does not accumulate edits.
        public static SceneDocument Dress(SceneDocument actor, RigDefinition rig, WardrobeDocument wardrobe, string outfitId)
        {
            if (actor == null || rig == null || wardrobe == null) throw new ArgumentNullException("actor, rig and wardrobe are required");
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
            SceneDocument result = HideRegions(actor, wardrobe, worn);
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

        private static SceneDocument HideRegions(SceneDocument actor, WardrobeDocument wardrobe, List<Worn> worn)
        {
            HashSet<string> hiddenNodes = new HashSet<string>(StringComparer.Ordinal);
            foreach (Worn item in worn)
                foreach (string regionId in item.Part.HiddenRegions)
                    foreach (BodyRegion region in wardrobe.Regions)
                        if (region.Id == regionId) hiddenNodes.UnionWith(region.MeshNodes);
            SceneDocument result = SceneCloning.Copy(actor);
            if (hiddenNodes.Count == 0) return result;
            int[] remap = new int[result.Meshes.Length];
            List<MeshInstance> kept = new List<MeshInstance>();
            for (int index = 0; index < result.Meshes.Length; index++)
            {
                bool hidden = hiddenNodes.Contains(result.Nodes[result.Meshes[index].Node].Id);
                remap[index] = hidden ? -1 : kept.Count;
                if (hidden == false) kept.Add(result.Meshes[index]);
            }
            if (result.Parameters != null)
            {
                foreach (SceneParameterBinding binding in result.Parameters.Bindings)
                {
                    if (binding.Mesh < 0) continue;
                    if (remap[binding.Mesh] < 0) throw new ArgumentException("A hidden mesh carries parameter binding " + binding.Property);
                    binding.Mesh = remap[binding.Mesh];
                }
            }
            result.Meshes = kept.ToArray();
            SceneValidation.Ensure(result);
            return result;
        }

        private sealed class Worn
        {
            public WardrobeSlot Slot;
            public WardrobePart Part;
            public PartVariant Variant;
        }
    }
}
