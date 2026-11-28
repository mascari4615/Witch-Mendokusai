using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace WitchMendokusai.Tests
{
	// 스킬 재료 목록 (Editor/SkillMaterials/skill-materials.json) 검증. 굽기 전에 잡아야 할 잘못을 데이터 단계에서
	public class SkillMaterialCatalogTests
	{
		private const string BASE = @"{
			""Look"": { ""BodyWhiten"": 0.3 },
			""Defaults"": { ""Damage"": 5, ""Interval"": 1.0 },
			""Forms"": { ""LineShot"": { ""ShotSpeed"": 18 }, ""NovaSelf"": { ""Radius"": 3, ""Layers"": [ { ""Kind"": ""Burst"", ""Seconds"": 0.4, ""RingSeconds"": 0.3 } ] } },
			""Elements"": { ""ice"": { ""r"": 0.4, ""g"": 0.8, ""b"": 1.0, ""Art"": ""Assets/x.asset"" } },
			""Recipes"": [ RECIPE ]
		}";

		private static List<SkillMaterialSpec> Parse(string recipe) => SkillMaterialCatalog.Parse(BASE.Replace("RECIPE", recipe));

		[Test]
		public void Project_catalog_is_valid()
		{
			List<SkillMaterialSpec> specs = SkillMaterialCatalog.Load();
			Assert.Greater(specs.Count, 0);
			foreach (SkillMaterialSpec spec in specs)
				Assert.IsTrue(SkillMaterialForms.Has(spec.Form), spec.Id);
		}

		[Test]
		public void Recipe_overrides_form_and_defaults()
		{
			SkillMaterialSpec spec = Parse(@"{ ""Id"": ""Skill_A"", ""Form"": ""LineShot"", ""Element"": ""ice"", ""Damage"": 9 }")[0];
			Assert.AreEqual(9, spec.Int("Damage"));
			Assert.AreEqual(1f, spec.Float("Interval"));
			Assert.AreEqual(18f, spec.Float("ShotSpeed"));
		}

		[Test]
		public void Typo_key_fails()
		{
			Assert.Throws<InvalidOperationException>(() => Parse(@"{ ""Id"": ""Skill_A"", ""Form"": ""LineShot"", ""Element"": ""ice"", ""Damge"": 9 }"));
		}

		[Test]
		public void Numeric_or_unknown_status_fails()
		{
			Assert.Throws<InvalidOperationException>(() => Parse(@"{ ""Id"": ""Skill_A"", ""Form"": ""LineShot"", ""Element"": ""ice"", ""Effects"": [ { ""Status"": ""3"", ""Seconds"": 1 } ] }"));
			Assert.Throws<InvalidOperationException>(() => Parse(@"{ ""Id"": ""Skill_A"", ""Form"": ""LineShot"", ""Element"": ""ice"", ""Effects"": [ { ""Status"": ""Sloww"", ""Seconds"": 1 } ] }"));
		}

		[Test]
		public void Unknown_form_element_or_bad_id_fails()
		{
			Assert.Throws<InvalidOperationException>(() => Parse(@"{ ""Id"": ""Skill_A"", ""Form"": ""Nope"", ""Element"": ""ice"" }"));
			Assert.Throws<InvalidOperationException>(() => Parse(@"{ ""Id"": ""Skill_A"", ""Form"": ""LineShot"", ""Element"": ""mud"" }"));
			Assert.Throws<InvalidOperationException>(() => Parse(@"{ ""Id"": ""a/b"", ""Form"": ""LineShot"", ""Element"": ""ice"" }"));
		}

		[Test]
		public void Layers_come_from_form_or_recipe_and_follow_form_radius()
		{
			SkillMaterialSpec fromForm = Parse(@"{ ""Id"": ""Skill_A"", ""Form"": ""NovaSelf"", ""Element"": ""ice"" }")[0];
			Assert.AreEqual(1, fromForm.Layers.Count);
			Assert.AreEqual(3f, fromForm.Layers[0].Radius, "겹 반지름을 빼면 모양 반지름");
			SkillMaterialSpec bigger = Parse(@"{ ""Id"": ""Skill_A"", ""Form"": ""NovaSelf"", ""Element"": ""ice"", ""Radius"": 5 }")[0];
			Assert.AreEqual(5f, bigger.Layers[0].Radius, "레시피가 반지름을 바꾸면 그림도 따라감");
			SkillMaterialSpec own = Parse(@"{ ""Id"": ""Skill_A"", ""Form"": ""NovaSelf"", ""Element"": ""ice"", ""Layers"": [ { ""Kind"": ""Burst"", ""Radius"": 2, ""Seconds"": 1, ""RingSeconds"": 1 } ] }")[0];
			Assert.AreEqual(2f, own.Layers[0].Radius);
			Assert.AreEqual(0, Parse(@"{ ""Id"": ""Skill_A"", ""Form"": ""LineShot"", ""Element"": ""ice"" }")[0].Layers.Count);
		}

		[Test]
		public void Bad_layers_fail_while_parsing()
		{
			// 탄 모양은 아직 겹을 받지 않음. 굽기 전에 목록 단계에서 실패
			Assert.Throws<InvalidOperationException>(() => Parse(@"{ ""Id"": ""Skill_A"", ""Form"": ""LineShot"", ""Element"": ""ice"", ""Layers"": [ { ""Kind"": ""Burst"", ""Seconds"": 1, ""RingSeconds"": 1 } ] }"));
			Assert.Throws<InvalidOperationException>(() => Parse(@"{ ""Id"": ""Skill_A"", ""Form"": ""NovaSelf"", ""Element"": ""ice"", ""Layers"": [ { ""Kind"": ""Nope"" } ] }"));
			Assert.Throws<InvalidOperationException>(() => Parse(@"{ ""Id"": ""Skill_A"", ""Form"": ""NovaSelf"", ""Element"": ""ice"", ""Layers"": [ { ""Kind"": ""Burst"", ""Seconds"": 1, ""RingSeconds"": 1, ""Colour"": 1 } ] }"));
			Assert.Throws<InvalidOperationException>(() => Parse(@"{ ""Id"": ""Skill_A"", ""Form"": ""NovaSelf"", ""Element"": ""ice"", ""Layers"": 3 }"));
			Assert.Throws<InvalidOperationException>(() => Parse(@"{ ""Id"": ""Skill_A"", ""Form"": ""NovaSelf"", ""Element"": ""ice"", ""Effects"": ""Slow"" }"));
		}

		[Test]
		public void CombatFx_layer_needs_asset_and_follows_form_radius()
		{
			SkillMaterialSpec spec = Parse(@"{ ""Id"": ""Skill_A"", ""Form"": ""NovaSelf"", ""Element"": ""ice"", ""Layers"": [ { ""Kind"": ""CombatFx"", ""Asset"": ""Assets/x.asset"", ""Seconds"": 3, ""Follow"": true, ""TurnsPerSecond"": 2 } ] }")[0];
			SkillMaterialLayer layer = spec.Layers[0];
			Assert.AreEqual("Assets/x.asset", layer.Asset);
			Assert.AreEqual(3f, layer.Radius, "반지름을 빼면 모양 반지름");
			Assert.AreEqual(3f, layer.Length, "길이를 빼면 반지름");
			Assert.IsTrue(layer.Follow);
			Assert.AreEqual(2f, layer.TurnsPerSecond);
			Assert.IsFalse(layer.OnBlast);
			Assert.Throws<InvalidOperationException>(() => Parse(@"{ ""Id"": ""Skill_A"", ""Form"": ""NovaSelf"", ""Element"": ""ice"", ""Layers"": [ { ""Kind"": ""CombatFx"", ""Seconds"": 3 } ] }"));
		}

		[Test]
		public void Blast_layer_only_on_strike_forms()
		{
			Assert.Throws<InvalidOperationException>(() => Parse(@"{ ""Id"": ""Skill_A"", ""Form"": ""NovaSelf"", ""Element"": ""ice"", ""Layers"": [ { ""Kind"": ""Burst"", ""Seconds"": 1, ""RingSeconds"": 1, ""OnBlast"": true } ] }"));
		}

		[Test]
		public void Element_without_art_fails()
		{
			Assert.Throws<InvalidOperationException>(() => SkillMaterialCatalog.Parse(BASE.Replace(@", ""Art"": ""Assets/x.asset""", "").Replace("RECIPE", @"{ ""Id"": ""Skill_A"", ""Form"": ""LineShot"", ""Element"": ""ice"" }")));
		}

		[Test]
		public void Missing_value_fails_when_read()
		{
			SkillMaterialSpec spec = Parse(@"{ ""Id"": ""Skill_A"", ""Form"": ""LineShot"", ""Element"": ""ice"" }")[0];
			Assert.Throws<InvalidOperationException>(() => spec.Float("Range"));
		}
	}
}
