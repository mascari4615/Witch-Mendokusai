using System.Collections.Generic;
using System.Linq;
using WitchMendokusai.Numerics;

namespace WitchMendokusai
{
	// 발전소(전력원) 명부 — 셀→PowerSourceData. GridData/RoadGraph/ZoneGrid/CityEconomy/CitizenRegistry 형제
	// (WorldStage 가 = new() 소유, NonSerialized 런타임 + ISavable 영속). 전력원 위치=영속 상태(어디 놨나 = save).
	// CityPaintManager 가 매일 이 소스들→인접도로→PowerGrid.ComputeEnergizedRoads 로 전력 전파(INC-5).
	// RoadGraph/ZoneGrid 동형 dict + 멱등 Add(페인트 캔버스).
	public class PowerSourceRegistry : ISavable<List<KeyValuePair<Vector3Int, PowerSourceData>>>
	{
		public Dictionary<Vector3Int, PowerSourceData> Sources { get; private set; } = new();

		public bool Has(Vector3Int cell)
		{
			return Sources.ContainsKey(cell);
		}

		// 멱등 덮어쓰기 (도로/존 페인트 동형).
		public void Add(Vector3Int cell, int range)
		{
			Sources[cell] = new PowerSourceData(range);
		}

		public void Remove(Vector3Int cell)
		{
			Sources.Remove(cell);
		}

		// ISavable — GridData 형제 동형(덮어쓰기 머지, WorldStage.Load 가 Clear 선행 = replace).
		public void Load(List<KeyValuePair<Vector3Int, PowerSourceData>> saveData)
		{
			foreach ((Vector3Int key, PowerSourceData value) in saveData)
			{
				Sources[key] = value;
			}
		}

		public List<KeyValuePair<Vector3Int, PowerSourceData>> Save()
		{
			return Sources.ToList();
		}
	}
}
