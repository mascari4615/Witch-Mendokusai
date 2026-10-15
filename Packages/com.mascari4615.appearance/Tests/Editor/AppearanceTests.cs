using System;
using System.Collections.Generic;
using Appearance.Serialization;
using Appearance.Unity;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace Appearance.Tests
{
    public sealed class AppearanceTests
    {
        private sealed class CountingTarget : IAppearanceTarget
        {
            public int Calls;

            public void Apply(IReadOnlyList<SurfaceColor> colors)
            {
                Calls++;
            }
        }

        private static AppearanceDocument Document()
        {
            return new AppearanceDocument
            {
                Id = "test-palette",
                Colors = new List<SurfaceColor>
                {
                    new SurfaceColor { Surface = "surface", Channel = "base", ColorSpace = "srgb", Rgba = new[] { 0.1f, 0.2f, 0.3f, 1f } }
                }
            };
        }

        [Test]
        public void InvalidDocumentDoesNotReachTarget()
        {
            AppearanceDocument document = Document();
            document.Colors[0].Rgba[1] = float.NaN;
            CountingTarget target = new CountingTarget();
            Assert.Throws<ArgumentException>(() => AppearanceApplication.Apply(document, target));
            Assert.That(target.Calls, Is.Zero);
        }

        [Test]
        public void DuplicateChannelsAreRejected()
        {
            AppearanceDocument document = Document();
            document.Colors.Add(document.Colors[0]);
            Assert.That(AppearanceApplication.Validate(document), Has.Count.EqualTo(1));
        }

        [Test]
        public void SerializationPreservesValues()
        {
            AppearanceDocument expected = Document();
            AppearanceDocument actual = AppearanceJson.Read(AppearanceJson.Write(expected));
            Assert.That(actual.Id, Is.EqualTo(expected.Id));
            Assert.That(actual.Colors[0].Rgba, Is.EqualTo(expected.Colors[0].Rgba));
        }

        [Test]
        public void UnknownAndMissingContractFieldsAreRejected()
        {
            string valid = AppearanceJson.Write(Document());
            Assert.Throws<JsonSerializationException>(() => AppearanceJson.Read(valid.Insert(1, "\"Foreign\": true,")));
            Assert.Throws<ArgumentException>(() => AppearanceJson.Read("{}"));
            Assert.Throws<ArgumentException>(() => AppearanceJson.Read(valid.Replace("\"Version\": 1", "\"Version\": 2")));
        }

        [Test]
        public void UnityConsumerOwnsSnapshot()
        {
            GameObject owner = new GameObject("Appearance test");
            try
            {
                AppearancePalette palette = owner.AddComponent<AppearancePalette>();
                AppearanceDocument document = Document();
                AppearanceApplication.Apply(document, palette);
                document.Colors[0].Rgba[0] = 0.9f;
                Assert.That(palette.Colors[0].Rgba[0], Is.EqualTo(0.1f));
                palette.Load(AppearanceJson.Write(Document()));
                Assert.That(palette.Colors.Count, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }
    }
}
