using System;
using System.Collections.Generic;
using System.IO;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai.Tests
{
	// 생활 규칙 시험의 공통 준비. 실제 자료 파일을 읽음 (사본 없음)
	internal static class HomesteadTestKit
	{
		internal sealed class FakePurse : IHomesteadPurse
		{
			public readonly Dictionary<int, int> Bag = new();
			public int Nyang { get; set; }

			public int CountItem(int itemId) => Bag.TryGetValue(itemId, out int amount) ? amount : 0;

			public int ConsumeItem(int itemId, int amount)
			{
				int taken = Math.Min(amount, CountItem(itemId));
				Bag[itemId] = CountItem(itemId) - taken;
				return amount - taken;
			}
		}

		internal static HomesteadCatalog LoadCatalog()
		{
			List<string> texts = new();
			string[] files = Directory.GetFiles(DataFolder(), "*.json");
			Array.Sort(files, StringComparer.Ordinal);
			foreach (string file in files)
			{
				texts.Add(File.ReadAllText(file));
			}
			return HomesteadCatalogJson.Load(texts);
		}

		internal static HomesteadSim MakeSim(out FakePurse purse, int seed = 7, HomesteadState state = null)
		{
			purse = new FakePurse { Nyang = 1000 };
			return new HomesteadSim(LoadCatalog(), state ?? new HomesteadState(), purse, new Random(seed));
		}

		// 값을 낼 수 있게 채움
		internal static void Afford(HomesteadSim sim, FakePurse purse, CostDef cost)
		{
			foreach (KeyValuePair<string, int> pair in cost.Goods)
			{
				sim.Give(pair.Key, pair.Value);
			}
			purse.Nyang += cost.Nyang;
		}

		internal static void BuildNow(HomesteadSim sim, FakePurse purse, string buildingId)
		{
			while (sim.State.TownHall < sim.Catalog.Get<BuildingDef>(buildingId).TownHall)
			{
				sim.TryGetNextTownHall(out TownHallDef next);
				Afford(sim, purse, next.Cost);
				sim.UpgradeTownHall();
			}
			sim.TryGetNextLevel(buildingId, out BuildingLevelDef level);
			Afford(sim, purse, level.Cost);
			sim.StartBuild(buildingId);
			sim.NotifyExpedition();
			sim.Unveil(buildingId);
		}

		private static string DataFolder() => FindUp(Path.Combine("Assets", "_WitchMendokusai", "Domain", "Homestead", "Resources", "Homestead"));

		// 생활 화면 스타일시트 원문. 배치 시험이 사본 없이 실제 수치를 읽음
		internal static string StyleSheetText() =>
			File.ReadAllText(Path.Combine(FindUp(Path.Combine("Assets", "_WitchMendokusai", "Domain", "Homestead", "UI")), "Homestead.uss"));

		private static string FindUp(string relative)
		{
			DirectoryInfo folder = new(Directory.GetCurrentDirectory());
			while (folder != null)
			{
				string candidate = Path.Combine(folder.FullName, relative);
				if (Directory.Exists(candidate))
				{
					return candidate;
				}
				folder = folder.Parent;
			}
			throw new DirectoryNotFoundException(relative);
		}
	}
}
