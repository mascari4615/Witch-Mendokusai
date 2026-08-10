using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using NUnit.Framework;
using WitchMendokusai;

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

		private sealed class GoldenTable
		{
			public Dictionary<string, string> itemNames { get; set; }
			public GoldenRow[] build { get; set; }
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
