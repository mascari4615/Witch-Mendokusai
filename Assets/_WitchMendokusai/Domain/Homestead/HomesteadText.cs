using System.Collections.Generic;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai
{
	// 규칙의 사유와 수를 사람 말로. 화면이 같이 씀
	public static class HomesteadText
	{
		private const int MINUTES_PER_HOUR = 60;

		private static readonly Dictionary<ToolKind, string> TOOL_NAMES = new()
		{
			{ ToolKind.Hoe, "괭이" },
			{ ToolKind.WateringCan, "물뿌리개" },
			{ ToolKind.Sickle, "낫" },
			{ ToolKind.Axe, "도끼" },
			{ ToolKind.Pickaxe, "곡괭이" },
			{ ToolKind.Rod, "낚싯대" },
		};

		private static readonly Dictionary<GoodsKind, string> KIND_NAMES = new()
		{
			{ GoodsKind.Material, "재료" },
			{ GoodsKind.Crop, "작물" },
			{ GoodsKind.Forage, "들" },
			{ GoodsKind.Fish, "물고기" },
			{ GoodsKind.Ore, "광석" },
			{ GoodsKind.Relic, "유물" },
			{ GoodsKind.Product, "가공" },
			{ GoodsKind.Dish, "음식" },
			{ GoodsKind.Seed, "씨앗" },
		};

		private static readonly string[] GRIME_NAMES = { "마른 흙", "녹", "엉킨 실" };
		private static readonly string[] BRUSH_NAMES = { "붓", "기름천", "바늘" };
		private static readonly string[] SEASON_NAMES = { "봄", "여름", "가을", "겨울" };

		public static string ToolName(ToolKind kind) => TOOL_NAMES[kind];
		public static string KindName(GoodsKind kind) => KIND_NAMES[kind];
		public static string Grime(int index) => GRIME_NAMES[index];
		public static string Brush(int index) => BRUSH_NAMES[index];
		public static int BrushCount => BRUSH_NAMES.Length;
		public static string Season(int index) => SEASON_NAMES[index % SEASON_NAMES.Length];

		public static string Minutes(long minutes)
		{
			if (minutes < MINUTES_PER_HOUR)
				return $"{minutes}분";
			long hours = minutes / MINUTES_PER_HOUR;
			long rest = minutes % MINUTES_PER_HOUR;
			return rest == 0 ? $"{hours}시간" : $"{hours}시간 {rest}분";
		}

		public static string Reason(HomesteadResult result, HomesteadCatalog catalog)
		{
			switch (result.Failure)
			{
				case HomesteadFailure.NotEnoughGoods:
					return result.Subject.Length == 0 ? "가진 것이 모자랍니다." : $"{catalog.Get<GoodsDef>(result.Subject).Name} 부족";
				case HomesteadFailure.NotEnoughNyang:
					return "냥이 부족합니다.";
				case HomesteadFailure.TownHallTooLow:
					return result.Subject.Length == 0 ? "회관을 더 키워야 합니다." : $"회관 {result.Subject}단 필요";
				case HomesteadFailure.NotBuilt:
					return catalog.TryGet(result.Subject, out BuildingDef building) ? $"{building.Name} 필요" : "아직 짓지 않았습니다.";
				case HomesteadFailure.NobodyThere:
					return "맡을 사람이 아직 없습니다.";
				case HomesteadFailure.AtMaxLevel:
					return "끝까지 키웠습니다.";
				case HomesteadFailure.AlreadyDone:
					return "이미 했습니다.";
				case HomesteadFailure.NotReady:
					return "아직입니다.";
				case HomesteadFailure.ToolTooWeak:
					return "도구가 약합니다.";
				case HomesteadFailure.NoRoom:
					return "자리가 없습니다.";
				case HomesteadFailure.Blocked:
					return "겹칩니다.";
				case HomesteadFailure.NotOwned:
					return "가지고 있지 않습니다.";
				case HomesteadFailure.DailyLimit:
					return "오늘은 이미 했습니다.";
				case HomesteadFailure.Busy:
					return "빈자리가 없습니다.";
				case HomesteadFailure.Locked:
					return "다른 길로 얻습니다.";
				case HomesteadFailure.LanternOut:
					return "등불이 다했습니다.";
				default:
					return string.Empty;
			}
		}

		public static string Reward(RewardDef reward, HomesteadCatalog catalog)
		{
			List<string> parts = new();
			if (reward.Nyang > 0)
				parts.Add($"{reward.Nyang}냥");
			foreach (KeyValuePair<string, int> pair in reward.Goods)
				parts.Add($"{catalog.Get<GoodsDef>(pair.Key).Name} {pair.Value}");
			foreach (string furniture in reward.Furniture)
				parts.Add(catalog.Get<FurnitureDef>(furniture).Name);
			foreach (string costume in reward.Costumes)
				parts.Add(catalog.Get<CostumeDef>(costume).Name);
			if (reward.GuildExp > 0)
				parts.Add($"길드 {reward.GuildExp}");
			return string.Join(", ", parts);
		}
	}
}
