using System;
using NUnit.Framework;
using Rigging;
using SceneContent;
using Wardrobe.Serialization;

namespace Wardrobe.Tests
{
    public sealed class WardrobeTests
    {
        [Test]
        public void DressingBindsSharedBonesHidesTheBodyAndAppliesVariants()
        {
            SceneDocument actor = Actor();
            SceneDocument casual = WardrobeApplication.Dress(actor, Rig(), Build(), "casual");
            Assert.That(casual.Nodes.Length, Is.EqualTo(7));
            Assert.That(casual.Meshes.Length, Is.EqualTo(3));
            Assert.That(casual.Meshes[0].Hidden, Is.True, "the body mesh stays in the document but is hidden");
            MeshInstance jacket = Array.Find(casual.Meshes, mesh => casual.Nodes[mesh.Node].Id == "jacket:jacket-mesh");
            Assert.That(jacket.Bones, Is.EqualTo(new[] { 1, 2 }), "the jacket binds to the actor's own hips and neck");
            Assert.That(casual.Nodes[Array.FindIndex(casual.Nodes, node => node.Id == "pendant:pendant")].Parent, Is.EqualTo(2));
            Assert.That(casual.Materials[casual.Meshes[2].Materials[0]].BaseColor[0], Is.EqualTo(1f), "the red variant");
            Assert.That(casual.Springs.Length, Is.EqualTo(1));
            Assert.That(casual.Springs[0].Joint, Is.EqualTo("pendant:pendant"));
        }

        [Test]
        public void SwitchingOutfitsOnADressedSceneEqualsDressingTheActor()
        {
            SceneDocument actor = Actor();
            string before = Text(actor);
            WardrobeDocument wardrobe = Build();
            SceneDocument casual = WardrobeApplication.Dress(actor, Rig(), wardrobe, "casual");
            SceneDocument formal = WardrobeApplication.Dress(actor, Rig(), wardrobe, "formal");
            Assert.That(Text(WardrobeApplication.Dress(casual, Rig(), wardrobe, "formal")), Is.EqualTo(Text(formal)));
            Assert.That(Text(WardrobeApplication.Dress(casual, Rig(), wardrobe, "plain")), Is.EqualTo(before), "taking everything off restores the actor exactly");
            Assert.That(Text(WardrobeApplication.Undress(formal, wardrobe)), Is.EqualTo(before));
            Assert.That(Text(actor), Is.EqualTo(before), "the input is never modified");
        }

        [Test]
        public void EachCharacterOfASceneIsDressedOnItsOwn()
        {
            SceneDocument actor = Actor();
            SceneDocument two = SceneComposition.Append(actor, actor, "actor-2", new float[3]);
            RigDefinition rig = new RigDefinition
            {
                Id = "rig2", Limbs = new RigLimb[0],
                Joints = new[]
                {
                    new RigJoint { Role = "hips", Node = "hips" }, new RigJoint { Role = "neck", Node = "neck" },
                    new RigJoint { Role = "actor-2:hips", Node = "actor-2:hips" }, new RigJoint { Role = "actor-2:neck", Node = "actor-2:neck" }
                }
            };
            WardrobeDocument wardrobe = Build();
            string before = Text(two);
            SceneDocument second = WardrobeApplication.Dress(two, rig, wardrobe, "casual", "actor-2");
            Assert.That(Array.Exists(second.Nodes, node => node.Id == "actor-2/pendant:pendant"), Is.True);
            Assert.That(Array.Exists(second.Nodes, node => node.Id == "pendant:pendant"), Is.False);
            SceneDocument both = WardrobeApplication.Dress(second, rig, wardrobe, "formal", "");
            Assert.That(Array.Exists(both.Nodes, node => node.Id == "pendant:pendant"), Is.True);
            SceneDocument firstOff = WardrobeApplication.Dress(both, rig, wardrobe, "plain", "");
            Assert.That(Text(firstOff), Is.EqualTo(Text(second)), "the second character keeps its outfit");
            Assert.That(Text(WardrobeApplication.Undress(firstOff, wardrobe, "actor-2")), Is.EqualTo(before));
        }

        [Test]
        public void AnchorOffsetCarriesThePartAndIsRemovedWithIt()
        {
            WardrobeDocument wardrobe = Build();
            wardrobe.Parts[1].AnchorOffset = new AnchorOffset { Position = new[] { 0f, .038f, -.008f }, Rotation = new[] { 0f, 0f, 0f, 1f } };
            SceneDocument actor = Actor();
            SceneDocument dressed = WardrobeApplication.Dress(actor, Rig(), wardrobe, "casual");
            SceneNode offset = Array.Find(dressed.Nodes, node => node.Id == "pendant:anchor-offset");
            Assert.That(offset, Is.Not.Null);
            Assert.That(dressed.Nodes[offset.Parent].Id, Is.EqualTo("neck"));
            Assert.That(offset.Position[1], Is.EqualTo(.038f).Within(1e-6));
            Assert.That(Text(WardrobeApplication.Undress(dressed, wardrobe)), Is.EqualTo(Text(actor)));
            wardrobe.Parts[1].AnchorOffset.Rotation = new[] { 2f, 0f, 0f, 1f };
            Assert.Throws<ArgumentException>(() => wardrobe.Validate());
        }

        [Test]
        public void CarrierMovesThePartWithAnAncestorWhileKeepingItAtTheAnchor()
        {
            WardrobeDocument wardrobe = Build();
            wardrobe.Parts[1].Carrier = "chest";
            wardrobe.Parts[1].AnchorOffset = new AnchorOffset { Position = new[] { 0f, .038f, 0f }, Rotation = new[] { 0f, 0f, 0f, 1f } };
            SceneDocument actor = ActorWithChest();
            RigDefinition rig = ChestRig();
            SceneDocument dressed = WardrobeApplication.Dress(actor, rig, wardrobe, "casual");
            SceneNode offset = Array.Find(dressed.Nodes, node => node.Id == "pendant:anchor-offset");
            Assert.That(dressed.Nodes[offset.Parent].Id, Is.EqualTo("chest"), "the part hangs from the carrier");
            Assert.That(offset.Position[1], Is.EqualTo(.438f).Within(1e-6), "and sits at the neck plus the offset");
            Assert.That(Text(WardrobeApplication.Undress(dressed, wardrobe)), Is.EqualTo(Text(actor)));

            SceneNode neck = Array.Find(actor.Nodes, node => node.Id == "neck");
            float half = (float)Math.Sqrt(.5);
            neck.Rotation = new[] { 0f, 0f, half, half };
            offset = Array.Find(WardrobeApplication.Dress(actor, rig, wardrobe, "casual").Nodes, node => node.Id == "pendant:anchor-offset");
            Assert.That(offset.Position[0], Is.EqualTo(-.038f).Within(1e-6), "a turned neck turns the offset in the carrier frame");
            Assert.That(offset.Position[1], Is.EqualTo(.4f).Within(1e-6));

            wardrobe.Parts[1].Carrier = "hips";
            Assert.Throws<ArgumentException>(() => WardrobeApplication.Dress(actor, rig, wardrobe, "casual"), "a carrier that is not an ancestor of the anchor");
            wardrobe.Parts[1].Carrier = "neck";
            Assert.Throws<ArgumentException>(() => wardrobe.Validate(), "the carrier equals the anchor");
            wardrobe.Parts[1].Carrier = "chest";
            Assert.That(WardrobeJson.Read(WardrobeJson.Write(wardrobe)).Parts[1].Carrier, Is.EqualTo("chest"));
        }

        [Test]
        public void InvalidOutfitsAreRejectedBeforeAnythingChanges()
        {
            SceneDocument actor = Actor();
            WardrobeDocument wardrobe = Build();
            Assert.Throws<ArgumentException>(() => Dress(wardrobe, actor, Pick("top", "jacket"), Pick("top", "jacket")), "a slot picked twice");
            Assert.Throws<ArgumentException>(() => Dress(wardrobe, actor, Pick("neck", "jacket")), "a part in the wrong slot");
            Assert.Throws<ArgumentException>(() => Dress(wardrobe, actor, Pick("top", "jacket", "missing")), "an unknown variant");
            wardrobe = Build();
            wardrobe.Slots[0].Conflicts = new[] { "neck" };
            wardrobe.Outfits = new[] { new Outfit { Id = "both", Picks = new[] { Pick("top", "jacket"), Pick("neck", "pendant") } } };
            Assert.Throws<ArgumentException>(() => WardrobeApplication.Dress(actor, Rig(), wardrobe, "both"), "conflicting slots");
            wardrobe = Build();
            wardrobe.Parts[1].Scene.Cameras = new[] { new SceneCamera { Node = 0 } };
            Assert.Throws<ArgumentException>(() => WardrobeApplication.Dress(actor, Rig(), wardrobe, "casual"), "a camera in a part");
            wardrobe = Build();
            wardrobe.Parts[0].Scene.Springs = new[] { new SceneSpring { Id = "bad", Joint = "hips", Tip = "jacket-mesh" } };
            Assert.Throws<ArgumentException>(() => WardrobeApplication.Dress(actor, Rig(), wardrobe, "casual"), "a spring on a shared host node");
            wardrobe = Build();
            wardrobe.Parts[1].Anchor = "missing-role";
            Assert.Throws<ArgumentException>(() => WardrobeApplication.Dress(actor, Rig(), wardrobe, "casual"), "an anchor role missing from the rig");
        }

        [Test]
        public void JsonRoundTripDressesTheSameAndUnknownFieldsAreRejected()
        {
            WardrobeDocument wardrobe = Build();
            string json = WardrobeJson.Write(wardrobe);
            WardrobeDocument reopened = WardrobeJson.Read(json);
            Assert.That(Text(WardrobeApplication.Dress(Actor(), Rig(), reopened, "casual")), Is.EqualTo(Text(WardrobeApplication.Dress(Actor(), Rig(), wardrobe, "casual"))));
            string surprising = json.Replace("\"Format\": \"wardrobe\"", "\"Format\": \"wardrobe\", \"Surprise\": 1");
            Assert.That(surprising, Is.Not.EqualTo(json), "the test must actually add the unknown field");
            Assert.Catch(() => WardrobeJson.Read(surprising));
        }

        private static void Dress(WardrobeDocument wardrobe, SceneDocument actor, params OutfitPick[] picks)
        {
            wardrobe.Outfits = new[] { new Outfit { Id = "x", Picks = picks } };
            WardrobeApplication.Dress(actor, Rig(), wardrobe, "x");
        }

        private static string Text(SceneDocument scene)
        {
            return SceneContent.Serialization.SceneJson.Write(scene);
        }

        private static OutfitPick Pick(string slot, string part, string variant = null)
        {
            return new OutfitPick { Slot = slot, Part = part, Variant = variant };
        }

        private static RigDefinition Rig()
        {
            return new RigDefinition
            {
                Id = "rig", Limbs = new RigLimb[0],
                Joints = new[] { new RigJoint { Role = "hips", Node = "hips" }, new RigJoint { Role = "neck", Node = "neck" } }
            };
        }

        private static RigDefinition ChestRig()
        {
            return new RigDefinition
            {
                Id = "rig", Limbs = new RigLimb[0],
                Joints = new[] { new RigJoint { Role = "hips", Node = "hips" }, new RigJoint { Role = "chest", Node = "chest" }, new RigJoint { Role = "neck", Node = "neck" } }
            };
        }

        // The neck hangs from a chest node, so a part can be carried by the chest and still sit at the neck.
        private static SceneDocument ActorWithChest()
        {
            return new SceneDocument
            {
                Id = "actor",
                Nodes = new[] { Node("root", -1, 0, 0, 0), Node("hips", 0, 0, 1f, 0), Node("chest", 0, 0, 1.2f, 0), Node("neck", 2, 0, .4f, 0), Node("body-mesh", 0, 0, 0, 0) },
                Meshes = new[] { new MeshInstance { Node = 4, Geometry = "g-body", Bones = new[] { 1, 3 }, Materials = new[] { 0 } } },
                Materials = new[] { Material(.8f, .6f, .5f) },
                Resources = new[] { Geometry("g-body") }
            };
        }

        private static SceneNode Node(string id, int parent, float x, float y, float z)
        {
            return new SceneNode { Id = id, Parent = parent, Position = new[] { x, y, z }, Rotation = new[] { 0f, 0f, 0f, 1f }, Scale = new[] { 1f, 1f, 1f } };
        }

        private static SurfaceMaterial Material(float r, float g, float b)
        {
            return new SurfaceMaterial { BaseColor = new[] { r, g, b, 1f } };
        }

        private static ContentResource Geometry(string id)
        {
            return new ContentResource { Id = id, Path = id + ".bin", MediaType = "application/vnd.native.geometry", Sha256 = new string('a', 64) };
        }

        private static SceneDocument Actor()
        {
            return new SceneDocument
            {
                Id = "actor",
                Nodes = new[] { Node("root", -1, 0, 0, 0), Node("hips", 0, 0, 1f, 0), Node("neck", 0, 0, 1.6f, 0), Node("body-mesh", 0, 0, 0, 0) },
                Meshes = new[] { new MeshInstance { Node = 3, Geometry = "g-body", Bones = new[] { 1, 2 }, Materials = new[] { 0 } } },
                Materials = new[] { Material(.8f, .6f, .5f) },
                Resources = new[] { Geometry("g-body") }
            };
        }

        private static WardrobeDocument Build()
        {
            SceneDocument jacket = new SceneDocument
            {
                Id = "jacket",
                Nodes = new[] { Node("hips", -1, 0, 0, 0), Node("neck", -1, 0, 0, 0), Node("jacket-mesh", -1, 0, 0, 0) },
                Meshes = new[] { new MeshInstance { Node = 2, Geometry = "g-jacket", Bones = new[] { 0, 1 }, Materials = new[] { 0 } } },
                Materials = new[] { Material(.2f, .2f, .2f) },
                Resources = new[] { Geometry("g-jacket") }
            };
            SceneDocument pendant = new SceneDocument
            {
                Id = "pendant",
                Nodes = new[] { Node("pendant", -1, 0, .1f, .05f), Node("pendant-tip", 0, 0, -.02f, 0) },
                Springs = new[] { new SceneSpring { Id = "swing", Joint = "pendant", Tip = "pendant-tip" } },
                Meshes = new[] { new MeshInstance { Node = 0, Geometry = "g-pendant", Bones = new int[0], Materials = new[] { 0 } } },
                Materials = new[] { Material(.9f, .9f, .1f) },
                Resources = new[] { Geometry("g-pendant") }
            };
            return new WardrobeDocument
            {
                Id = "wardrobe",
                Slots = new[] { new WardrobeSlot { Id = "top", Layer = 1 }, new WardrobeSlot { Id = "neck", Layer = 2 } },
                Regions = new[] { new BodyRegion { Id = "torso", MeshNodes = new[] { "body-mesh" } } },
                Parts = new[]
                {
                    new WardrobePart
                    {
                        Id = "jacket", Slot = "top", Anchor = "hips", Scene = jacket, HiddenRegions = new[] { "torso" },
                        Bindings = new[] { new PartBinding { PartNode = "hips", Role = "hips" }, new PartBinding { PartNode = "neck", Role = "neck" } }
                    },
                    new WardrobePart
                    {
                        Id = "pendant", Slot = "neck", Anchor = "neck", Scene = pendant,
                        Variants = new[]
                        {
                            new PartVariant { Id = "red", Materials = new[] { new MaterialOverride { Material = 0, BaseColor = new[] { 1f, 0f, 0f, 1f } } } },
                            new PartVariant { Id = "blue", Materials = new[] { new MaterialOverride { Material = 0, BaseColor = new[] { 0f, 0f, 1f, 1f } } } }
                        }
                    }
                },
                Outfits = new[]
                {
                    new Outfit { Id = "casual", Picks = new[] { Pick("neck", "pendant", "red"), Pick("top", "jacket") } },
                    new Outfit { Id = "formal", Picks = new[] { Pick("top", "jacket"), Pick("neck", "pendant", "blue") } },
                    new Outfit { Id = "plain" }
                }
            };
        }
    }
}
