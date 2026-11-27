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
			""Forms"": { ""LineShot"": { ""ShotSpeed"": 18 } },
			""Elements"": { ""ice"": { ""r"": 0.4, ""g"": 0.8, ""b"": 1.0 } },
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
		public void Missing_value_fails_when_read()
		{
			SkillMaterialSpec spec = Parse(@"{ ""Id"": ""Skill_A"", ""Form"": ""LineShot"", ""Element"": ""ice"" }")[0];
			Assert.Throws<InvalidOperationException>(() => spec.Float("Range"));
		}
	}
}
