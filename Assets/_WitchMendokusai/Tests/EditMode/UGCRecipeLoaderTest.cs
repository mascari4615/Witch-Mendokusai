using Newtonsoft.Json;
using NUnit.Framework;
using WitchMendokusai.DomainSDK.Alchemy;
using WitchMendokusai.DomainSDK.UGC;

namespace WitchMendokusai.Tests
{
	public sealed class UGCRecipeLoaderTest
	{
		private const string SAMPLE_FILE = "recipe_001_starlight_salve.json";

		[Test]
		public void TryLoadRecipeManifestFromSample_StarlightSalve_ReturnsExpectedFields()
		{
			bool ok = UGCJsonLoader.TryLoadRecipeManifestFromSample(SAMPLE_FILE, out UGCRecipeManifestData manifest, out string error);

			Assert.That(ok, Is.True, $"sample 로딩 실패: {error}");
			Assert.That(manifest, Is.Not.Null);
			Assert.That(manifest.schemaVersion, Is.EqualTo(1));
			Assert.That(manifest.manifestId, Is.EqualTo("recipe_001_starlight_salve"));
			Assert.That(manifest.author, Is.EqualTo("WM_System"));
			Assert.That(manifest.recipe, Is.Not.Null);
			Assert.That(manifest.recipe.title, Is.EqualTo("별빛 연고"));
			Assert.That(manifest.recipe.effectName, Is.EqualTo("Restore"));
			Assert.That(manifest.recipe.target.radius, Is.EqualTo(0.4f).Within(0.0001f));
			Assert.That(manifest.recipe.ingredients, Is.Not.Null);
			Assert.That(manifest.recipe.ingredients.Count, Is.EqualTo(2));
			Assert.That(manifest.recipe.ingredients[0].ingredientId, Is.EqualTo(101));
		}

		[Test]
		public void ToEffectTarget_TransfersPositionAndRadius()
		{
			UGCRecipeTargetData target = new()
			{
				positionX = 1.2f,
				positionY = -0.5f,
				radius = 0.3f,
			};

			EffectTarget effect = target.ToEffectTarget();

			Assert.That(effect.Position.X, Is.EqualTo(1.2f).Within(0.0001f));
			Assert.That(effect.Position.Y, Is.EqualTo(-0.5f).Within(0.0001f));
			Assert.That(effect.Radius, Is.EqualTo(0.3f).Within(0.0001f));
		}

		[Test]
		public void TryValidateRecipeManifest_RejectsWrongSchemaVersion()
		{
			UGCRecipeManifestData manifest = MakeValidManifest();
			manifest.schemaVersion = 99;

			bool ok = UGCJsonValidator.TryValidateRecipeManifest(manifest, out string error);

			Assert.That(ok, Is.False);
			Assert.That(error, Does.Contain("schemaVersion"));
		}

		[Test]
		public void TryValidateRecipeManifest_RejectsEmptyManifestId()
		{
			UGCRecipeManifestData manifest = MakeValidManifest();
			manifest.manifestId = "";

			bool ok = UGCJsonValidator.TryValidateRecipeManifest(manifest, out string error);

			Assert.That(ok, Is.False);
			Assert.That(error, Does.Contain("manifestId"));
		}

		[Test]
		public void TryValidateRecipeManifest_RejectsNullRecipe()
		{
			UGCRecipeManifestData manifest = MakeValidManifest();
			manifest.recipe = null;

			bool ok = UGCJsonValidator.TryValidateRecipeManifest(manifest, out string error);

			Assert.That(ok, Is.False);
			Assert.That(error, Does.Contain("recipe page"));
		}

		[Test]
		public void TryValidateRecipeManifest_RejectsZeroRadius()
		{
			UGCRecipeManifestData manifest = MakeValidManifest();
			manifest.recipe.target.radius = 0f;

			bool ok = UGCJsonValidator.TryValidateRecipeManifest(manifest, out string error);

			Assert.That(ok, Is.False);
			Assert.That(error, Does.Contain("radius"));
		}

		[Test]
		public void TryValidateRecipeManifest_RejectsNegativeGrind()
		{
			UGCRecipeManifestData manifest = MakeValidManifest();
			manifest.recipe.ingredients.Add(new UGCRecipeIngredientRefData
			{
				ingredientId = 101,
				grind = -0.1f,
			});

			bool ok = UGCJsonValidator.TryValidateRecipeManifest(manifest, out string error);

			Assert.That(ok, Is.False);
			Assert.That(error, Does.Contain("grind"));
		}

		[Test]
		public void TryValidateRecipeManifest_RejectsInvertedThresholds()
		{
			UGCRecipeManifestData manifest = MakeValidManifest();
			manifest.recipe.gradeThresholds = new UGCRecipeGradeThresholdsData
			{
				crudeMaxDistance = 1.0f,
				fineMaxDistance = 1.5f,
				masterworkMaxDistance = 0.3f,
				masterworkMaxSideEffect = 0.1f,
			};

			bool ok = UGCJsonValidator.TryValidateRecipeManifest(manifest, out string error);

			Assert.That(ok, Is.False);
			Assert.That(error, Does.Contain("fineMaxDistance"));
		}

		[Test]
		public void Deserialize_DropsOutOfSchemaFields()
		{
			string maliciousJson = @"{
				""schemaVersion"": 1,
				""manifestId"": ""recipe_sandbox_probe"",
				""version"": 1,
				""author"": ""tester"",
				""recipe"": {
					""recipeId"": 9,
					""effectName"": ""Probe"",
					""title"": ""Sandbox Probe"",
					""target"": { ""positionX"": 0, ""positionY"": 0, ""radius"": 0.5 },
					""ingredients"": [],
					""arbitraryEvilField"": ""attempt-to-escape""
				},
				""tags"": [],
				""__exec"": ""os.system('rm -rf /')""
			}";

			JsonSerializerSettings settings = new()
			{
				MissingMemberHandling = MissingMemberHandling.Ignore,
				NullValueHandling = NullValueHandling.Include,
			};

			UGCRecipeManifestData parsed = JsonConvert.DeserializeObject<UGCRecipeManifestData>(maliciousJson, settings);

			Assert.That(parsed, Is.Not.Null);
			Assert.That(parsed.manifestId, Is.EqualTo("recipe_sandbox_probe"));
			Assert.That(parsed.recipe.title, Is.EqualTo("Sandbox Probe"));

			System.Reflection.FieldInfo evilField = typeof(UGCRecipePageData).GetField("arbitraryEvilField");
			Assert.That(evilField, Is.Null, "UGCRecipePageData 에 arbitraryEvilField 가 생기면 sandbox 표면 확장 — 의도적 결정 필요");

			System.Reflection.FieldInfo execField = typeof(UGCRecipeManifestData).GetField("__exec");
			Assert.That(execField, Is.Null, "UGCRecipeManifestData 에 __exec 가 생기면 sandbox 우회 표면 — 절대 추가 X");
		}

		private static UGCRecipeManifestData MakeValidManifest()
		{
			return new UGCRecipeManifestData
			{
				schemaVersion = 1,
				manifestId = "recipe_test",
				version = 1,
				author = "tester",
				recipe = new UGCRecipePageData
				{
					recipeId = 1,
					effectName = "Restore",
					title = "Test Recipe",
					description = "for test",
					target = new UGCRecipeTargetData
					{
						positionX = 1f,
						positionY = 1f,
						radius = 0.5f,
					},
					ingredients = new(),
					gradeThresholds = null,
					tags = new(),
				},
			};
		}
	}
}
