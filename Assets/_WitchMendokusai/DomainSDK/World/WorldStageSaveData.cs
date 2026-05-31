using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	[Serializable]
	public struct WorldStageSaveData
	{
		public List<KeyValuePair<Vector3Int, BuildingInstanceData>> BuildingSaveData;

		// SimCity Phase 1 (TASK-WM-164): 도로/존 레이어. 옛 세이브엔 부재 → Load 시 null 가드
		// (WorldStage.Load). GridData 와 같은 직렬화 형태(List<KVP<Vector3Int, T>>).
		public List<KeyValuePair<Vector3Int, RoadCellData>> RoadSaveData;
		public List<KeyValuePair<Vector3Int, ZoneCellData>> ZoneSaveData;

		// SimCity Phase 2 (TASK-WM-166 INC-4): GlassBox 경제 상태(자원 재고). 옛 세이브엔 부재 →
		// default(struct).StockSaveData = null → CityEconomy.Load 자체 skip. wrapper struct(미래 필드 확장).
		public CityEconomySaveData EconomySaveData;

		// SimCity Phase 2 (TASK-WM-166 INC-6): 통근 시민 명부. 옛 세이브엔 부재(null) → WorldStage.Load null skip.
		public List<CitizenSaveData> CitizensSaveData;
	}
}
