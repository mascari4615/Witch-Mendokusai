using NUnit.Framework;
using System.Collections.Generic;
using WitchMendokusai.DomainSDK.Alchemy;

namespace WitchMendokusai.Tests
{
    /// <summary>
    /// TASK-WM-186 — 마도서 레시피 UGC 로딩 first-use 회귀 락.
    /// 팬 JSON → DomainSDK UGCRecipeManifest schema → BrewRecipe (플랫포머 UGC 재조준 → 마도서 핵심 루프).
    /// "사용자 데이터는 DomainSDK 정의 schema 만 채움" 약속 실현 + sandbox. 게임 소비(CauldronMap)는 후속 증분.
    /// </summary>
    public sealed class UGCRecipeLoaderTest
    {
        [Test]
        public void FanRecipeJson_ParsesIntoBrewRecipe()
        {
            string json = @"{
                ""schemaVersion"": 1,
                ""author"": ""fan_yon"",
                ""recipes"": [
                    { ""id"": 90001, ""effectName"": ""따뜻함"", ""targetX"": 1.5, ""targetY"": -2.0, ""radius"": 0.5 }
                ]
            }";

            bool ok = UGCRecipeLoader.TryLoad(json, out List<BrewRecipe> recipes, out string error);

            Assert.That(ok, Is.True, error);
            Assert.That(recipes.Count, Is.EqualTo(1));
            BrewRecipe r = recipes[0];
            Assert.That(r.Id, Is.EqualTo(90001));
            Assert.That(r.EffectName, Is.EqualTo("따뜻함"));
            Assert.That(r.Target.Position.X, Is.EqualTo(1.5f).Within(0.0001f));
            Assert.That(r.Target.Position.Y, Is.EqualTo(-2.0f).Within(0.0001f));
            Assert.That(r.Target.Radius, Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void SandboxRejects_InvalidRadius()
        {
            string json = @"{ ""recipes"": [ { ""id"": 90002, ""effectName"": ""x"", ""radius"": 0 } ] }";
            bool ok = UGCRecipeLoader.TryLoad(json, out _, out string error);
            Assert.That(ok, Is.False);
            Assert.That(error, Is.Not.Null);
        }

        [Test]
        public void EmptyJson_Rejected()
        {
            bool ok = UGCRecipeLoader.TryLoad("", out _, out _);
            Assert.That(ok, Is.False);
        }
    }
}
