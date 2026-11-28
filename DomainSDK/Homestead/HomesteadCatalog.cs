using System;
using System.Collections.Generic;

namespace WitchMendokusai.DomainSDK.Homestead
{
	// 생활 컨텐츠의 정의 묶음. 자료 파일 (json) 하나가 이 모양 하나
	// 찾기는 전부 여기를 거침. 없는 id 는 던짐 (자료 오류를 조용히 넘기지 않음)
	public sealed class HomesteadCatalog
	{
		public HomesteadTuning Tuning { get; set; } = new HomesteadTuning();
		public List<GoodsDef> Goods { get; set; } = new List<GoodsDef>();
		public List<CropDef> Crops { get; set; } = new List<CropDef>();
		public List<GatherDef> Gathers { get; set; } = new List<GatherDef>();
		public List<FishDef> Fishes { get; set; } = new List<FishDef>();
		public List<MineBandDef> MineBands { get; set; } = new List<MineBandDef>();
		public List<MineNodeDef> MineNodes { get; set; } = new List<MineNodeDef>();
		public List<RelicDef> Relics { get; set; } = new List<RelicDef>();
		public List<WingDef> Wings { get; set; } = new List<WingDef>();
		public List<ToolDef> Tools { get; set; } = new List<ToolDef>();
		public List<TownHallDef> TownHalls { get; set; } = new List<TownHallDef>();
		public List<BuildingDef> Buildings { get; set; } = new List<BuildingDef>();
		public List<ResidentDef> Residents { get; set; } = new List<ResidentDef>();
		public List<JobDef> Jobs { get; set; } = new List<JobDef>();
		public List<RecipeDef> Recipes { get; set; } = new List<RecipeDef>();
		public List<CookDef> Cooks { get; set; } = new List<CookDef>();
		public List<SpellDef> Spells { get; set; } = new List<SpellDef>();
		public List<RequestDef> Requests { get; set; } = new List<RequestDef>();
		public List<HouseTierDef> HouseTiers { get; set; } = new List<HouseTierDef>();
		public List<FurnitureDef> Furniture { get; set; } = new List<FurnitureDef>();
		public List<CostumeDef> Costumes { get; set; } = new List<CostumeDef>();
		public List<AnimalDef> Animals { get; set; } = new List<AnimalDef>();
		public List<PlaceableDef> Placeables { get; set; } = new List<PlaceableDef>();
		public VillageAreaDef VillageArea { get; set; } = new VillageAreaDef();
		public List<LayoutDef> Layout { get; set; } = new List<LayoutDef>();

		private readonly Dictionary<Type, Dictionary<string, HomesteadDef>> indexes = new Dictionary<Type, Dictionary<string, HomesteadDef>>();

		public T Get<T>(string id) where T : HomesteadDef => (T)IndexOf<T>()[id];

		public bool TryGet<T>(string id, out T found) where T : HomesteadDef
		{
			found = null;
			if (id == null || IndexOf<T>().TryGetValue(id, out HomesteadDef raw) == false)
			{
				return false;
			}
			found = (T)raw;
			return true;
		}

		public ToolDef Tool(ToolKind kind)
		{
			foreach (ToolDef tool in Tools)
			{
				if (tool.Kind == kind)
				{
					return tool;
				}
			}
			throw new KeyNotFoundException($"도구 {kind} 정의 없음");
		}

		private Dictionary<string, HomesteadDef> IndexOf<T>() where T : HomesteadDef
		{
			if (indexes.TryGetValue(typeof(T), out Dictionary<string, HomesteadDef> index))
			{
				return index;
			}
			index = new Dictionary<string, HomesteadDef>();
			foreach (HomesteadDef definition in ListOf(typeof(T)))
			{
				index.Add(definition.Id, definition);
			}
			indexes.Add(typeof(T), index);
			return index;
		}

		private IEnumerable<HomesteadDef> ListOf(Type type)
		{
			if (type == typeof(GoodsDef)) return Goods;
			if (type == typeof(CropDef)) return Crops;
			if (type == typeof(GatherDef)) return Gathers;
			if (type == typeof(FishDef)) return Fishes;
			if (type == typeof(MineBandDef)) return MineBands;
			if (type == typeof(MineNodeDef)) return MineNodes;
			if (type == typeof(RelicDef)) return Relics;
			if (type == typeof(WingDef)) return Wings;
			if (type == typeof(ToolDef)) return Tools;
			if (type == typeof(BuildingDef)) return Buildings;
			if (type == typeof(ResidentDef)) return Residents;
			if (type == typeof(JobDef)) return Jobs;
			if (type == typeof(RecipeDef)) return Recipes;
			if (type == typeof(CookDef)) return Cooks;
			if (type == typeof(SpellDef)) return Spells;
			if (type == typeof(RequestDef)) return Requests;
			if (type == typeof(FurnitureDef)) return Furniture;
			if (type == typeof(CostumeDef)) return Costumes;
			if (type == typeof(AnimalDef)) return Animals;
			if (type == typeof(PlaceableDef)) return Placeables;
			throw new ArgumentException($"목록 없는 정의 {type.Name}");
		}
	}
}
