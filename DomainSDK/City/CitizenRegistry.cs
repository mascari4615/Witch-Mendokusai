using System.Collections.Generic;
using System.Linq;

namespace WitchMendokusai
{
	// 도시 시민 명부 — 통근 시민들의 영속 데이터. GridData/RoadGraph/ZoneGrid/CityEconomy 형제(WorldStage 소유,
	// NonSerialized 런타임 + ISavable 영속). INC-7 이동이 여기 시민을 spawn/추종(집↔직장). INC-6 = 데이터+영속만.
	public class CitizenRegistry : ISavable<List<CitizenSaveData>>
	{
		public List<CitizenSaveData> Citizens { get; private set; } = new();

		public void Add(CitizenSaveData citizen)
		{
			Citizens.Add(citizen);
		}

		// ISavable — merge add (WorldStage.Load 가 Clear 선행, GridData 형제 동형 = replace 의미).
		public void Load(List<CitizenSaveData> saveData)
		{
			foreach (CitizenSaveData citizen in saveData)
			{
				Citizens.Add(citizen);
			}
		}

		public List<CitizenSaveData> Save()
		{
			return Citizens.ToList();
		}
	}
}
