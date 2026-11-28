using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Homestead
{
	// 벌통이 내는 꿀 한 가지. 벌통 둘레의 꽃 가운데 제일 많은 것을 따라감
	public sealed class HoneyDef : HomesteadDef
	{
		// 거둘 때 받는 물건
		public string Goods { get; set; } = string.Empty;
		// 꽃 자리. 마을 꾸밈 id (flowerbed) 나 작물 id (pumpkin). 비면 둘레에 꽃이 없을 때의 기본 꿀
		public List<string> Flowers { get; set; } = new List<string>();
	}

	// 꿀 자료 점검. 꽃 id 가 꾸밈이나 작물을 가리키는지, 기본 꿀이 하나인지
	public static class HoneyCheck
	{
		public static void Run(HomesteadCatalog catalog, List<string> problems)
		{
			if (catalog.Honeys.Count == 0)
			{
				return;
			}
			int plain = 0;
			foreach (HoneyDef honey in catalog.Honeys)
			{
				if (catalog.TryGet(honey.Goods, out GoodsDef _) == false)
				{
					problems.Add($"꿀 {honey.Id}: 물건 '{honey.Goods}' 없음");
				}
				if (honey.Flowers.Count == 0)
				{
					plain++;
				}
				foreach (string flower in honey.Flowers)
				{
					if (catalog.TryGet(flower, out PlaceableDef _) == false && catalog.TryGet(flower, out CropDef _) == false)
					{
						problems.Add($"꿀 {honey.Id}: 꽃 '{flower}' 는 꾸밈도 작물도 아님");
					}
				}
			}
			if (plain != 1)
			{
				problems.Add($"꽃 없이 나오는 기본 꿀이 {plain}개 (하나여야 함)");
			}
			if (catalog.TryGet(HomesteadSim.HIVE, out PlaceableDef _) == false)
			{
				problems.Add($"꾸밈 '{HomesteadSim.HIVE}' 없음");
			}
			if (catalog.TryGet(HomesteadSim.BEESWAX, out GoodsDef _) == false)
			{
				problems.Add($"물건 '{HomesteadSim.BEESWAX}' 없음");
			}
		}

		// 벌통에서 나오는 물건. 나오는 곳 점검 (CheckSources) 이 씀
		public static IEnumerable<string> Outputs(HomesteadCatalog catalog)
		{
			if (catalog.Honeys.Count == 0)
			{
				yield break;
			}
			yield return HomesteadSim.BEESWAX;
			foreach (HoneyDef honey in catalog.Honeys)
			{
				yield return honey.Goods;
			}
		}
	}
}
