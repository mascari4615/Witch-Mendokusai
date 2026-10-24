using System;
using NUnit.Framework;
using SceneContent;
using SceneContent.Unity;
using UnityEngine;

namespace WitchMendokusai.ContentRuntime.Tests
{
    public sealed class ToonSurfaceFactoryTests
    {
        [Test]
        public void ToonCapabilityAndStoredInputsAreRequiredAndMaterialInstancesAreOwned()
        {
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            Shader toon = Shader.Find("WM/Toon Surface");
            Assert.That(toon, Is.Not.Null);
            SurfaceMaterial surface = new SurfaceMaterial
            {
                BaseColor = new[] { 1f, 1f, 1f, 1f }, LightingModel = "toon", AlphaMode = "mask", DoubleSided = true,
                ToonSurface = new ToonSurface { BandStrength = .3f, OutlineWidth = .004f, RimPower = 4 }
            };
            Assert.Throws<NotSupportedException>(() => new StandardSurfaceFactory(unlit, lit).Create(surface, null));
            Assert.Throws<NotSupportedException>(() => new SingleShaderSurfaceFactory(unlit).Create(surface, null));
            StandardSurfaceFactory factory = new StandardSurfaceFactory(unlit, lit, toon);
            Material first = factory.Create(surface, null);
            Material second = factory.Create(surface, null);
            try
            {
                Assert.That(first.shader, Is.SameAs(toon));
                Assert.That(first.GetFloat("_BandStrength"), Is.EqualTo(.3f));
                Assert.That(first.GetFloat("_OutlineWidth"), Is.EqualTo(.004f));
                Assert.That(first.GetFloat("_RimPower"), Is.EqualTo(4));
                Assert.That(first.GetColor("_ShadowColor").r, Is.EqualTo(.8f));
                Assert.That(first.GetFloat("_AlphaClip"), Is.EqualTo(1));
                Assert.That(first.GetFloat("_Cull"), Is.EqualTo(0));
                first.SetFloat("_RimPower", 7);
                Assert.That(second.GetFloat("_RimPower"), Is.EqualTo(4));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
            }
        }
    }
}
