using System;
using System.Collections.Generic;
using NUnit.Framework;
using SceneContent.Serialization;

namespace SceneContent.Tests
{
    public sealed class ToonSurfaceTests
    {
        private static SceneDocument Document()
        {
            SceneDocument document = SceneFixtures.Document(SceneFixtures.Bytes());
            document.Version = 2;
            document.Materials[0].LightingModel = "toon";
            document.Materials[0].MaterialClass = "hair";
            document.Materials[0].ToonSurface = new ToonSurface { BandStrength = .3f, OutlineWidth = .004f, RimPower = 4 };
            return document;
        }

        [Test]
        public void OldDocumentsKeepVersionAndDoNotEmitNewFields()
        {
            SceneDocument document = SceneFixtures.Document(SceneFixtures.Bytes());
            string json = SceneJson.Write(document);
            Assert.That(json, Does.Not.Contain("ToonSurface"));
            Assert.That(json, Does.Not.Contain("MaterialClass"));
            Assert.That(SceneJson.Read(json).Version, Is.EqualTo(1));
            document.Materials[0].MaterialClass = "hair";
            Assert.Throws<ArgumentException>(() => SceneJson.Write(document));
        }

        [Test]
        public void ToonRoundTripSnapshotCompositionAndCopyRetainOwnedValues()
        {
            SceneDocument source = Document();
            SceneDocument loaded = SceneJson.Read(SceneJson.Write(source));
            Assert.That(loaded.Version, Is.EqualTo(2));
            Assert.That(loaded.Materials[0].MaterialClass, Is.EqualTo("hair"));
            Assert.That(loaded.Materials[0].ToonSurface.BandStrength, Is.EqualTo(.3f));
            SceneDocument copy = SceneCloning.Copy(loaded);
            copy.Materials[0].ToonSurface.ShadeTint[0] = .1f;
            copy.Materials[0].ToonSurface.OutlineTint[0] = .2f;
            Assert.That(loaded.Materials[0].ToonSurface.ShadeTint[0], Is.EqualTo(.8f));
            Assert.That(loaded.Materials[0].ToonSurface.OutlineTint[0], Is.EqualTo(.35f));
            SceneDocument original = SceneFixtures.Document(SceneFixtures.Bytes());
            Assert.That(SceneComposition.Append(original, loaded, "actor", new float[3]).Version, Is.EqualTo(2));
            SceneDocument attached = SceneComposition.Attach(original, loaded, "part", original.Nodes[0].Id, new Dictionary<string, string>());
            Assert.That(attached.Version, Is.EqualTo(2));
            Assert.That(ScenePruning.Snapshot(loaded, new string[0]).Materials[0].ToonSurface.RimPower, Is.EqualTo(4));
        }

        [Test]
        public void InvalidModelsVersionsAndBlocksAreRejected()
        {
            SceneDocument document = Document();
            document.Version = 1;
            Assert.Throws<ArgumentException>(() => SceneValidation.Ensure(document));
            document.Version = 2;
            document.Materials[0].LightingModel = "lit";
            Assert.Throws<ArgumentException>(() => SceneValidation.Ensure(document));
            document.Materials[0].LightingModel = "toon";
            document.Materials[0].ToonSurface = null;
            Assert.Throws<ArgumentException>(() => SceneValidation.Ensure(document));
            document.Materials[0].ToonSurface = new ToonSurface();
            document.Materials[0].MaterialClass = "unknown";
            Assert.Throws<ArgumentException>(() => SceneValidation.Ensure(document));
            string json = SceneJson.Write(Document());
            Assert.That(() => SceneJson.Read(json.Replace("\"BandStrength\"", "\"UnknownBand\"")), Throws.Exception);
        }

        [Test]
        public void AllToonControlsEnforceFiniteRangesAndRgbShapes()
        {
            foreach (Action<ToonSurface> mutate in new Action<ToonSurface>[]
            {
                value => value.ShadeTint = new float[4], value => value.OutlineTint[0] = -1,
                value => value.ShadeExtent = float.NaN, value => value.ShadeSoftness = 0,
                value => value.ShadeRichness = 2, value => value.LightStrength = float.PositiveInfinity,
                value => value.AmbientStrength = -1, value => value.RimStrength = 2,
                value => value.RimPower = .1f, value => value.BandStrength = -1,
                value => value.BandHeight = 2, value => value.BandWidth = 0, value => value.OutlineWidth = .03f
            })
            {
                ToonSurface value = new ToonSurface();
                mutate(value);
                Assert.Throws<ArgumentException>(() => value.Validate());
            }
        }

    }
}
