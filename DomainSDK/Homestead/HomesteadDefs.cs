using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Homestead
{
	public abstract class HomesteadDef
	{
		public string Id { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;
		public string Note { get; set; } = string.Empty;
	}

	// 값. 물건과 냥을 함께 요구 가능
	public sealed class CostDef
	{
		public int Nyang { get; set; }
		public Dictionary<string, int> Goods { get; set; } = new Dictionary<string, int>();
	}

	// 무게를 단 뽑기 한 줄
	public sealed class DropDef
	{
		public string Goods { get; set; } = string.Empty;
		public int Min { get; set; } = 1;
		public int Max { get; set; } = 1;
		public float Weight { get; set; } = 1f;
	}

	// 창고 물건. ItemId 가 있으면 던전 가방의 같은 물건도 재료로 셈
	public sealed class GoodsDef : HomesteadDef
	{
		public GoodsKind Kind { get; set; }
		public int ItemId { get; set; } = -1;
		public int Price { get; set; }
		public int Grade { get; set; }
	}

	public sealed class CropDef : HomesteadDef
	{
		public string Seed { get; set; } = string.Empty;
		public string Harvest { get; set; } = string.Empty;
		public int GrowMinutes { get; set; }
		public int Stages { get; set; } = 4;
		public int YieldMin { get; set; } = 1;
		public int YieldMax { get; set; } = 1;
		// 0 이면 한 번 거두고 끝. 그 밖에는 거둔 뒤 다시 익는 데 걸리는 분
		public int RegrowMinutes { get; set; }
		// 제철. 제철이면 수확이 하나 더. 철이 아니어도 시들지 않음
		public List<int> Seasons { get; set; } = new List<int>();
		public int SeedPrice { get; set; }
		public int TownHall { get; set; } = 1;
	}

	// 나무, 덤불, 돌무더기 같은 채집 자리
	public sealed class GatherDef : HomesteadDef
	{
		public ToolKind Tool { get; set; }
		public int Tier { get; set; }
		public int Hits { get; set; } = 1;
		public int RespawnMinutes { get; set; }
		public int Rolls { get; set; } = 1;
		// 굴림 전에 늘 주는 몫. 나무를 베면 늘 나무가 나옴. 굴림은 덤
		public List<DropDef> Fixed { get; set; } = new List<DropDef>();
		public List<DropDef> Drops { get; set; } = new List<DropDef>();
		public float RelicChance { get; set; }
	}

	public sealed class FishDef : HomesteadDef
	{
		public string Goods { get; set; } = string.Empty;
		public List<string> Waters { get; set; } = new List<string>();
		public int HourFrom { get; set; }
		public int HourTo { get; set; } = 24;
		public List<int> Seasons { get; set; } = new List<int>();
		public float Weight { get; set; } = 1f;
		public int RodTier { get; set; }
		// 힘은 줄 부담, 변덕은 몸부림 사이가 짧아짐. 0..1
		public float Strength { get; set; }
		public float Temper { get; set; }
		public float SizeMin { get; set; }
		public float SizeMax { get; set; }
		public float RelicChance { get; set; }
	}

	// 갱도의 깊이 구간. 구간마다 나오는 것이 다름
	public sealed class MineBandDef : HomesteadDef
	{
		public int From { get; set; }
		public int To { get; set; }
		public int Nodes { get; set; }
		public List<MineWeightDef> Weights { get; set; } = new List<MineWeightDef>();
		public float RelicChance { get; set; }
		// 바위 타수 배율. 깊을수록 단단해져 좋은 곡괭이가 더 깊이 감 (확정 f04-1, 구간마다 +25%)
		public float Hardness { get; set; } = 1f;
		// 화면 색 이름. 표현 층이 해석
		public string Mood { get; set; } = string.Empty;
	}

	public sealed class MineWeightDef
	{
		public string Node { get; set; } = string.Empty;
		public float Weight { get; set; } = 1f;
	}

	public sealed class MineNodeDef : HomesteadDef
	{
		public int Tier { get; set; }
		public int Hits { get; set; } = 3;
		public List<DropDef> Drops { get; set; } = new List<DropDef>();
	}

	public sealed class RelicDef : HomesteadDef
	{
		// 손질 도구 수. Grime 값은 0 부터 이 수 아래
		public const int TOOLS = 3;

		public string Goods { get; set; } = string.Empty;
		public string Set { get; set; } = string.Empty;
		public float Weight { get; set; } = 1f;
		// 손질 순서. 0 붓, 1 기름천, 2 바늘
		public List<int> Grime { get; set; } = new List<int>();
	}

	// 박물관의 관 하나. 갈래 하나와 이정표 보상
	public sealed class WingDef : HomesteadDef
	{
		public List<GoodsKind> Kinds { get; set; } = new List<GoodsKind>();
		// 갈래 밖인데 이 관이 받는 물건. 목장 산물처럼 갈래가 가공품과 섞인 것
		public List<string> Exhibits { get; set; } = new List<string>();
		public List<MilestoneDef> Milestones { get; set; } = new List<MilestoneDef>();
	}

	public sealed class MilestoneDef
	{
		public int Count { get; set; }
		public RewardDef Reward { get; set; } = new RewardDef();
	}

	public sealed class RewardDef
	{
		public int Nyang { get; set; }
		public int GuildExp { get; set; }
		public Dictionary<string, int> Goods { get; set; } = new Dictionary<string, int>();
		public List<string> Furniture { get; set; } = new List<string>();
		public List<string> Costumes { get; set; } = new List<string>();
	}

	public sealed class ToolDef : HomesteadDef
	{
		public ToolKind Kind { get; set; }
		public List<ToolTierDef> Tiers { get; set; } = new List<ToolTierDef>();
	}

	// Power 의 뜻은 도구마다. 괭이와 물뿌리개는 한 번에 닿는 밭 칸, 도끼와 곡괭이는 타격, 낚싯대는 줄 힘
	public sealed class ToolTierDef
	{
		public string Name { get; set; } = string.Empty;
		public int Power { get; set; } = 1;
		public CostDef Cost { get; set; } = new CostDef();
		public int TownHall { get; set; } = 1;
	}

	public sealed class TownHallDef
	{
		public int Tier { get; set; }
		public string Name { get; set; } = string.Empty;
		public string Note { get; set; } = string.Empty;
		public CostDef Cost { get; set; } = new CostDef();
		// 같이 벌일 수 있는 공사 수
		public int WorkSlots { get; set; } = 1;
		public int PlotCount { get; set; }
		public RewardDef Reward { get; set; } = new RewardDef();
	}

	public sealed class BuildingDef : HomesteadDef
	{
		public int TownHall { get; set; } = 1;
		// 비면 사람 없이 열림. 있으면 그 주민이 와야 기능이 열림
		public string Resident { get; set; } = string.Empty;
		public List<BuildingLevelDef> Levels { get; set; } = new List<BuildingLevelDef>();
	}

	public sealed class BuildingLevelDef
	{
		public CostDef Cost { get; set; } = new CostDef();
		public int TownHall { get; set; } = 1;
		public int Minutes { get; set; } = 240;
		public string Effect { get; set; } = string.Empty;
		public int GuildExp { get; set; }
	}

	public sealed class ResidentDef : HomesteadDef
	{
		public string Building { get; set; } = string.Empty;
		public List<RecruitDef> Recruits { get; set; } = new List<RecruitDef>();
		// 단계마다 필요한 누적 점수. 길이가 그 주민의 단계 수
		public List<int> AffinitySteps { get; set; } = new List<int>();
		public List<RewardDef> AffinityRewards { get; set; } = new List<RewardDef>();
		public List<GoodsKind> Likes { get; set; } = new List<GoodsKind>();
		public List<string> Favorites { get; set; } = new List<string>();
		public List<string> Lines { get; set; } = new List<string>();
	}

	public sealed class RecruitDef
	{
		public RecruitKind Kind { get; set; }
		public string Key { get; set; } = string.Empty;
		public int Count { get; set; }
		public CostDef Cost { get; set; } = new CostDef();
		public string Hint { get; set; } = string.Empty;
	}

	// 인형에게 맡기는 일. 한 자리에 인형 하나
	public sealed class JobDef : HomesteadDef
	{
		public string Building { get; set; } = string.Empty;
		public int IntervalMinutes { get; set; } = 60;
		public int Cap { get; set; } = 10;
		public List<DropDef> Yields { get; set; } = new List<DropDef>();
		// 밭 물 주기처럼 생산이 아닌 일
		public string Perk { get; set; } = string.Empty;
	}

	public sealed class RecipeDef : HomesteadDef
	{
		public string Building { get; set; } = string.Empty;
		public int Level { get; set; } = 1;
		public Dictionary<string, int> Inputs { get; set; } = new Dictionary<string, int>();
		public string Output { get; set; } = string.Empty;
		public int Amount { get; set; } = 1;
		public int Minutes { get; set; } = 60;
	}

	public sealed class RequestDef : HomesteadDef
	{
		public int GuildLevel { get; set; } = 1;
		public List<string> Goods { get; set; } = new List<string>();
		public int Min { get; set; } = 1;
		public int Max { get; set; } = 1;
		public float Pay { get; set; } = 1.6f;
		public int Exp { get; set; }
	}

	// 마을 배치물. 칸은 복셀과 같은 1m. 건물은 하나뿐이고 옮기기만
	public sealed class PlaceableDef : HomesteadDef
	{
		public PlaceableKind Kind { get; set; }
		public int Width { get; set; } = 1;
		public int Depth { get; set; } = 1;
		// 물가처럼 자리가 정해진 것
		public bool Fixed { get; set; }
		public int TownHall { get; set; } = 1;
		public CostDef Cost { get; set; } = new CostDef();
		// 모델 이름. 건물은 상태마다 달라 화면이 고름
		public List<string> Models { get; set; } = new List<string>();
	}

	public sealed class VillageAreaDef
	{
		public int X { get; set; }
		public int Z { get; set; }
		public int Width { get; set; }
		public int Depth { get; set; }
	}

	// 처음 마을의 배치 한 줄. 길과 담은 이어진 개수
	public sealed class LayoutDef
	{
		public string Id { get; set; } = string.Empty;
		public int X { get; set; }
		public int Z { get; set; }
		public int Turn { get; set; }
		public int CountX { get; set; } = 1;
		public int CountZ { get; set; } = 1;
	}

	public sealed class HouseTierDef
	{
		public int Tier { get; set; }
		public string Name { get; set; } = string.Empty;
		public float Width { get; set; }
		public float Depth { get; set; }
		public int PieceLimit { get; set; }
		public CostDef Cost { get; set; } = new CostDef();
	}

	public sealed class FurnitureDef : HomesteadDef
	{
		public FurnitureLayer Layer { get; set; }
		public string Theme { get; set; } = string.Empty;
		public float Width { get; set; } = 1f;
		public float Depth { get; set; } = 1f;
		public int Comfort { get; set; } = 1;
		public CostDef Cost { get; set; } = new CostDef();
		// 나는 곳. bench 집 제작대, workshop 공작소, shop 상점, reward 보상 전용
		public string Source { get; set; } = string.Empty;
		public int Level { get; set; } = 1;
		public string Shape { get; set; } = string.Empty;
	}

	public sealed class CostumeDef : HomesteadDef
	{
		public CostumeSlot Slot { get; set; }
		public CostDef Cost { get; set; } = new CostDef();
		public string Source { get; set; } = string.Empty;
		public int Level { get; set; } = 1;
		public string Look { get; set; } = string.Empty;
	}

	public sealed class AnimalDef : HomesteadDef
	{
		public CostDef Cost { get; set; } = new CostDef();
		public string Product { get; set; } = string.Empty;
		public string FineProduct { get; set; } = string.Empty;
		public int IntervalMinutes { get; set; } = 720;
		public int RanchLevel { get; set; } = 1;
		public int FineAffinity { get; set; } = 60;
	}
}
