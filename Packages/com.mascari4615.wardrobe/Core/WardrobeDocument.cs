using System;
using System.Collections.Generic;
using SceneContent;

namespace Wardrobe
{
    [Serializable]
    public sealed class WardrobeDocument
    {
        public string Format = "wardrobe";
        public int Version = 1;
        public string Id;
        public WardrobeSlot[] Slots = new WardrobeSlot[0];
        public BodyRegion[] Regions = new BodyRegion[0];
        public WardrobePart[] Parts = new WardrobePart[0];
        public Outfit[] Outfits = new Outfit[0];

        public void Validate()
        {
            if (Format != "wardrobe" || Version != 1 || string.IsNullOrEmpty(Id) || Slots == null || Regions == null || Parts == null ||
                Outfits == null || Slots.Length > 256 || Regions.Length > 256 || Parts.Length > 4096 || Outfits.Length > 4096)
                throw new ArgumentException("Invalid wardrobe header");
            HashSet<string> slots = new HashSet<string>(StringComparer.Ordinal);
            foreach (WardrobeSlot slot in Slots)
                if (slot == null || string.IsNullOrEmpty(slot.Id) || slots.Add(slot.Id) == false || slot.Conflicts == null)
                    throw new ArgumentException("Slots need unique ids");
            foreach (WardrobeSlot slot in Slots)
                foreach (string other in slot.Conflicts)
                    if (other == slot.Id || slots.Contains(other) == false) throw new ArgumentException("Slot conflict must name another existing slot");
            HashSet<string> regions = new HashSet<string>(StringComparer.Ordinal);
            foreach (BodyRegion region in Regions)
                if (region == null || string.IsNullOrEmpty(region.Id) || regions.Add(region.Id) == false || region.MeshNodes == null || region.MeshNodes.Length == 0)
                    throw new ArgumentException("Body regions need unique ids and at least one mesh node");
            HashSet<string> parts = new HashSet<string>(StringComparer.Ordinal);
            foreach (WardrobePart part in Parts)
            {
                if (part == null || string.IsNullOrEmpty(part.Id) || parts.Add(part.Id) == false || slots.Contains(part.Slot) == false ||
                    string.IsNullOrEmpty(part.Anchor) || part.Scene == null || part.Bindings == null || part.HiddenRegions == null || part.Variants == null)
                    throw new ArgumentException("Parts need a unique id, an existing slot, an anchor role and a scene");
                SceneValidation.Ensure(part.Scene);
                HashSet<string> nodes = new HashSet<string>(StringComparer.Ordinal);
                foreach (SceneNode node in part.Scene.Nodes) nodes.Add(node.Id);
                HashSet<string> bound = new HashSet<string>(StringComparer.Ordinal);
                foreach (PartBinding binding in part.Bindings)
                    if (binding == null || string.IsNullOrEmpty(binding.Role) || nodes.Contains(binding.PartNode) == false || bound.Add(binding.PartNode) == false)
                        throw new ArgumentException("Part bindings must name distinct nodes of the part");
                foreach (string region in part.HiddenRegions)
                    if (regions.Contains(region) == false) throw new ArgumentException("Part hides an unknown body region: " + region);
                HashSet<string> variants = new HashSet<string>(StringComparer.Ordinal);
                foreach (PartVariant variant in part.Variants)
                {
                    if (variant == null || string.IsNullOrEmpty(variant.Id) || variants.Add(variant.Id) == false || variant.Materials == null)
                        throw new ArgumentException("Variants need unique ids");
                    foreach (MaterialOverride change in variant.Materials)
                    {
                        if (change == null || change.Material < 0 || change.Material >= part.Scene.Materials.Length || change.BaseColor == null || change.BaseColor.Length != 4)
                            throw new ArgumentException("Variant material override is out of range");
                        foreach (float value in change.BaseColor)
                            if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentException("Variant color must be finite");
                    }
                }
            }
            HashSet<string> outfits = new HashSet<string>(StringComparer.Ordinal);
            foreach (Outfit outfit in Outfits)
            {
                if (outfit == null || string.IsNullOrEmpty(outfit.Id) || outfits.Add(outfit.Id) == false || outfit.Picks == null)
                    throw new ArgumentException("Outfits need unique ids");
            }
        }

        public WardrobePart FindPart(string id)
        {
            foreach (WardrobePart part in Parts) if (part.Id == id) return part;
            throw new ArgumentException("Unknown part: " + id);
        }
    }

    [Serializable]
    public sealed class WardrobeSlot
    {
        public string Id;
        public int Layer;
        public string[] Conflicts = new string[0];
    }

    [Serializable]
    public sealed class BodyRegion
    {
        public string Id;
        public string[] MeshNodes;
    }

    [Serializable]
    public sealed class WardrobePart
    {
        public string Id;
        public string Label;
        public string Slot;
        public string Anchor;
        public PartBinding[] Bindings = new PartBinding[0];
        public string[] HiddenRegions = new string[0];
        public PartVariant[] Variants = new PartVariant[0];
        public SceneDocument Scene;
    }

    [Serializable]
    public sealed class PartBinding
    {
        public string PartNode;
        public string Role;
    }

    [Serializable]
    public sealed class PartVariant
    {
        public string Id;
        public MaterialOverride[] Materials = new MaterialOverride[0];
    }

    [Serializable]
    public sealed class MaterialOverride
    {
        public int Material;
        public float[] BaseColor;
    }

    [Serializable]
    public sealed class Outfit
    {
        public string Id;
        public string Label;
        public OutfitPick[] Picks = new OutfitPick[0];
    }

    [Serializable]
    public sealed class OutfitPick
    {
        public string Slot;
        public string Part;
        public string Variant;
    }
}
