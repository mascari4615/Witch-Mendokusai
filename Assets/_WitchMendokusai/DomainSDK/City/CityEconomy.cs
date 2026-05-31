using System.Collections.Generic;
using System.Linq;

namespace WitchMendokusai
{
	// GlassBox 도시 전역 경제 상태 — 자원 누계 재고 원장. GridData/RoadGraph/ZoneGrid 형제(WorldStage 가
	// = new() 소유, NonSerialized 런타임 + ISavable 영속). INC-5 일일 틱이 생산/소비 결과를 여기 누적,
	// RciDemand 가 재고 부족을 성장 신호로 읽는다.
	//
	// 재고 키 = ResourceId(데이터주도) — 모드/UGC 가 정의한 새 자원도 그대로 누적(6 동기 모딩/UGC).
	// 음수 방지는 호출자 책임(BuildingProductionModel 가동률이 가용 재고를 상한으로 둠) — 여기선 산술만.
	public class CityEconomy : ISavable<CityEconomySaveData>
	{
		public Dictionary<ResourceId, float> Stock { get; private set; } = new();

		public float GetStock(ResourceId resource)
		{
			return Stock.TryGetValue(resource, out float amount) ? amount : 0f;
		}

		// 재고 증감 (생산 = +, 소비 = -). 0 으로 떨어져도 키 유지 — 카탈로그 존재성과 재고량은 별개.
		public void AddStock(ResourceId resource, float delta)
		{
			Stock[resource] = GetStock(resource) + delta;
		}

		// ISavable — replace (WorldStage.Load 가 Clear 선행, GridData 형제 동형). legacy(StockSaveData null) skip.
		public void Load(CityEconomySaveData saveData)
		{
			if (saveData.StockSaveData == null)
			{
				return;
			}

			foreach ((int resourceId, float amount) in saveData.StockSaveData)
			{
				Stock[new ResourceId(resourceId)] = amount;
			}
		}

		public CityEconomySaveData Save()
		{
			return new CityEconomySaveData
			{
				StockSaveData = Stock.Select(entry => new KeyValuePair<int, float>(entry.Key.Value, entry.Value)).ToList()
			};
		}
	}
}
