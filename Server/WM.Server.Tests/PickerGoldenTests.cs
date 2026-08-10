using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using NUnit.Framework;
using WitchMendokusai;
using WitchMendokusai.DomainSDK.Alchemy;

namespace WitchMendokusai.ServerTests
{
	/// <summary>
	/// <b>두 창이 같은 답을 내나</b> — 골든 표와 대조 (TASK-WM-217).
	///
	/// ★ 왜: 「지을 수 있나 · 뭐라고 보여 줄까」 규칙이 <b>두 벌</b>이다 — 웹 창은 자바스크립트로,
	///   게임 창은 C# 으로. 두 벌은 언젠가 갈라지고, 그러면 같은 세계에서 웹은 「지을 수 있다」,
	///   게임은 「못 짓는다」가 된다. 그건 같은 세계가 아니다.
	///
	/// 그래서 답을 <b>한 곳</b>(wwwroot/picker-golden.json)에 적어 두고 양쪽이 각자 그 표와 대조한다.
	/// 규칙을 바꾸려면 표부터 바꾼다 — 그러면 안 고친 쪽이 그 자리에서 빨개진다.
	/// (웹 쪽 대조 = <c>.github/scripts/wm-web-picker-test.mjs</c>)
	/// </summary>
	public sealed class PickerGoldenTests
	{
		private sealed class GoldenKind
		{
			public int buildingId { get; set; }
			public string name { get; set; }
			public int w { get; set; }
			public int l { get; set; }
			public int costItemId { get; set; }
			public int costAmount { get; set; }
		}

		private sealed class GoldenBag
		{
			public int itemId { get; set; }
			public int amount { get; set; }
		}

		private sealed class GoldenRow
		{
			public string @case { get; set; }
			public GoldenKind kind { get; set; }
			public GoldenBag[] bag { get; set; }
			public string label { get; set; }
			public bool canBuild { get; set; }
		}

		private sealed class GoldenRecipe
		{
			public int recipeId { get; set; }
			public string name { get; set; }
			public float percentage { get; set; }
			public int[] itemIds { get; set; }
			public int[] amounts { get; set; }
		}

		private sealed class GoldenCraftRow
		{
			public string @case { get; set; }
			public GoldenRecipe recipe { get; set; }
			public GoldenBag[] bag { get; set; }
			public string label { get; set; }
			public bool canCraft { get; set; }
		}

		private sealed class GoldenAimPage
		{
			public int id { get; set; }
			public string name { get; set; }
			public float targetX { get; set; }
			public float targetY { get; set; }
			public float radius { get; set; }
			public int amount { get; set; }
		}

		private sealed class GoldenAt
		{
			public float x { get; set; }
			public float y { get; set; }
		}

		private sealed class GoldenAimRow
		{
			public string @case { get; set; }
			public GoldenAimPage[] pages { get; set; }
			public GoldenAt at { get; set; }
			public int aimedId { get; set; }
			public string text { get; set; }
		}

		private sealed class GoldenTable
		{
			public Dictionary<string, string> itemNames { get; set; }
			public GoldenRow[] build { get; set; }
			public GoldenCraftRow[] craft { get; set; }
			public GoldenAimRow[] aim { get; set; }
		}

		[Test]
		public void 게임_쪽_고르개가_골든_표와_같은_답을_낸다()
		{
			GoldenTable golden = Load();
			Assert.IsNotNull(golden?.build, "골든 표를 못 읽었다 — 그러면 두 창을 묶을 방법이 없다");
			Assert.Greater(golden.build.Length, 0);

			foreach (GoldenRow row in golden.build)
			{
				BuildingCatalogEntry entry = new BuildingCatalogEntry
				{
					id = row.kind.buildingId,
					name = row.kind.name,
					w = row.kind.w,
					l = row.kind.l,
					costItemId = row.kind.costItemId,
					costAmount = row.kind.costAmount,
				};

				Dictionary<int, int> bag = new Dictionary<int, int>();
				foreach (GoldenBag carried in row.bag)
					bag[carried.itemId] = carried.amount;

				List<BuildOption> options = BuildAffordability.Options(
					new[] { entry },
					itemId => bag.TryGetValue(itemId, out int amount) ? amount : 0);

				Assert.AreEqual(1, options.Count, row.@case);
				Assert.AreEqual(row.canBuild, options[0].Affordable, $"「{row.@case}」 — 지을 수 있나가 다르다");

				// 글은 <b>같은 뜻</b>이어야 한다: 재료를 얼마나 들고 있고 얼마가 드는지.
				// (웹은 이름·크기까지 한 줄로 붙이고, 게임은 칸이 따로라 재료 부분만 맞춘다.)
				string cost = BuildAffordability.CostText(options[0], id =>
					golden.itemNames.TryGetValue(id.ToString(), out string named) ? named : null);

				if (row.kind.costAmount <= 0)
				{
					Assert.AreEqual(string.Empty, cost, $"「{row.@case}」 — 공짜인데 재료를 붙였다");
					continue;
				}

				StringAssert.Contains(cost, row.label,
					$"「{row.@case}」 — 게임이 보여 주는 재료({cost})가 웹의 글({row.label}) 안에 없다");
			}
		}

		[Test]
		public void 게임_쪽_제작_고르개가_골든_표와_같은_답을_낸다()
		{
			GoldenTable golden = Load();
			Assert.IsNotNull(golden?.craft, "골든 표에 제작 줄이 없다");

			foreach (GoldenCraftRow row in golden.craft)
			{
				CraftIngredientEntry[] items = new CraftIngredientEntry[row.recipe.itemIds.Length];
				for (int i = 0; i < items.Length; i++)
					items[i] = new CraftIngredientEntry { itemId = row.recipe.itemIds[i], amount = row.recipe.amounts[i] };

				CraftRecipeEntry recipe = new CraftRecipeEntry
				{
					id = row.recipe.recipeId,
					name = row.recipe.name,
					percentage = row.recipe.percentage,
					items = items,
				};

				Dictionary<int, int> bag = new Dictionary<int, int>();
				foreach (GoldenBag carried in row.bag)
					bag[carried.itemId] = carried.amount;

				int Carrying(int itemId) => bag.TryGetValue(itemId, out int amount) ? amount : 0;
				string Named(int itemId) =>
					golden.itemNames.TryGetValue(itemId.ToString(), out string named) ? named : null;

				Assert.AreEqual(row.canCraft, CraftAffordability.CanCraft(recipe, Carrying),
					$"「{row.@case}」 — 만들 수 있나가 웹과 다르다");

				Assert.AreEqual(row.label, CraftAffordability.Label(recipe, Carrying, Named),
					$"「{row.@case}」 — 보여 주는 글이 웹과 다르다");
			}
		}

		[Test]
		public void 두_창이_같은_쪽을_노린다()
		{
			// ★ 왜: 「지금 무엇을 만드는 중인가」를 웹은 자바스크립트로, 게임은 판정 층으로 고른다.
			//   두 벌이 갈라지면 웹은 「석재 쪽」, 게임은 「물약 쪽」을 가리키면서 <b>같은 솥</b>을 그린다.
			//   글자 모양은 창마다 달라도 되지만, <b>어느 쪽을 노리는가</b>는 반드시 같아야 한다.
			GoldenTable golden = Load();
			Assert.IsNotNull(golden?.aim, "골든 표에 겨냥 줄이 없다");
			Assert.Greater(golden.aim.Length, 0);

			foreach (GoldenAimRow row in golden.aim)
			{
				RecipeCatalogEntry[] pages = new RecipeCatalogEntry[row.pages.Length];
				for (int i = 0; i < pages.Length; i++)
				{
					GoldenAimPage page = row.pages[i];
					pages[i] = new RecipeCatalogEntry
					{
						id = page.id,
						name = page.name,
						targetX = page.targetX,
						targetY = page.targetY,
						radius = page.radius,
						resultItemId = page.id,
						amount = page.amount,
					};
				}

				BrewState state = new BrewState
				{
					Position = new BrewVector(row.at.x, row.at.y),
					StepCount = 1,
				};

				bool aiming = WorldSpellbookView.TryAim(pages, state, out BrewRecipe aimed);

				// aimedId 0 = 노릴 것이 없다(마도서가 비었다).
				Assert.AreEqual(row.aimedId != 0, aiming,
					$"「{row.@case}」 — 노릴 것이 있나가 웹과 다르다");

				if (aiming == false)
					continue;

				Assert.AreEqual(row.aimedId, aimed.Id,
					$"「{row.@case}」 — 게임이 노리는 쪽이 웹({row.text})과 다르다");
			}
		}

		private static GoldenTable Load()
		{
			// 서버 옆에 함께 나가는 파일이라, 시험은 저장소 원본을 그대로 읽는다.
			string path = Path.GetFullPath(Path.Combine(
				System.AppContext.BaseDirectory, "..", "..", "..", "..",
				"WM.Server", "wwwroot", "picker-golden.json"));

			Assert.IsTrue(File.Exists(path), $"골든 표가 없다: {path}");

			return JsonSerializer.Deserialize<GoldenTable>(File.ReadAllText(path),
				new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
		}
	}
}
