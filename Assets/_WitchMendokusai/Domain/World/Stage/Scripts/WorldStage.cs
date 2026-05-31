using System;
using UnityEngine;

namespace WitchMendokusai
{
	[CreateAssetMenu(fileName = "WS_", menuName = "WM/Data/" + nameof(WorldStage))]
	public class WorldStage : Stage, ISavable<WorldStageSaveData>
	{
		[field: NonSerialized] public GridData GridData { get; private set; } = new();

		// SimCity Phase 1 (TASK-WM-164): 도로/존 레이어. GridData 형제 — 같은 셀 좌표계(Vector3Int,
		// z=0) 공유, 책임 분리. WorldStage 가 셋 다 소유(NonSerialized 런타임, ISavable 로 영속).
		[field: NonSerialized] public RoadGraph RoadGraph { get; private set; } = new();
		[field: NonSerialized] public ZoneGrid ZoneGrid { get; private set; } = new();

		// SimCity Phase 2 (TASK-WM-166 INC-4): GlassBox 경제 상태(자원 누계 재고). 도시 레이어 형제 —
		// WorldStage 가 소유(NonSerialized 런타임, ISavable 영속). INC-5 일일 틱이 생산/소비를 여기 누적.
		[field: NonSerialized] public CityEconomy CityEconomy { get; private set; } = new();

		// SimCity Phase 2 (TASK-WM-166 INC-6): 통근 시민 명부. 도시 레이어 형제. INC-7 이동이 여기서 spawn/추종.
		[field: NonSerialized] public CitizenRegistry CitizenRegistry { get; private set; } = new();

		// SimCity Phase 3 (TASK-WM-176 INC-3): 발전소(전력원) 명부. 도시 레이어 형제. INC-5 가 매일 전력 전파 시드.
		[field: NonSerialized] public PowerSourceRegistry PowerSourceRegistry { get; private set; } = new();

		public void Load(WorldStageSaveData saveData)
		{
			// Load = replace, not merge. WorldStage 는 SO 자산이라 인스턴스가 stage 재진입/재로드
			// 간 살아남음 → 비우지 않으면 이전 도시 데이터가 누적(예: 게임 두 번 로드). 전부 clear 선행.
			GridData.BuildingData.Clear();
			RoadGraph.RoadData.Clear();
			ZoneGrid.ZoneData.Clear();
			CityEconomy.Stock.Clear();
			CitizenRegistry.Citizens.Clear();
			PowerSourceRegistry.Sources.Clear();

			GridData.Load(saveData.BuildingSaveData);

			// 옛 세이브엔 Road/Zone/Economy 필드 부재(null) — 이전 도시는 도로/존/경제 없음. null skip.
			if (saveData.RoadSaveData != null)
			{
				RoadGraph.Load(saveData.RoadSaveData);
			}
			if (saveData.ZoneSaveData != null)
			{
				ZoneGrid.Load(saveData.ZoneSaveData);
			}

			// EconomySaveData 는 struct(항상 비-null) — 내부 StockSaveData null 가드는 CityEconomy.Load 가 자체 처리(legacy 세이브 = default → skip).
			CityEconomy.Load(saveData.EconomySaveData);

			// 옛 세이브엔 시민 부재(null) — null skip.
			if (saveData.CitizensSaveData != null)
			{
				CitizenRegistry.Load(saveData.CitizensSaveData);
			}

			// 옛 세이브엔 발전소 부재(null) — null skip.
			if (saveData.PowerSourceSaveData != null)
			{
				PowerSourceRegistry.Load(saveData.PowerSourceSaveData);
			}
		}

		public WorldStageSaveData Save()
		{
			return new WorldStageSaveData()
			{
				BuildingSaveData = GridData.Save(),
				RoadSaveData = RoadGraph.Save(),
				ZoneSaveData = ZoneGrid.Save(),
				EconomySaveData = CityEconomy.Save(),
				CitizensSaveData = CitizenRegistry.Save(),
				PowerSourceSaveData = PowerSourceRegistry.Save()
			};
		}
	}
}