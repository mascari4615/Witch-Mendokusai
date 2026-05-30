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

		public void Load(WorldStageSaveData saveData)
		{
			GridData.Load(saveData.BuildingSaveData);

			// 옛 세이브엔 Road/Zone 필드 부재(null) — Phase 1 이전 도시는 도로/존 없음. null skip.
			if (saveData.RoadSaveData != null)
			{
				RoadGraph.Load(saveData.RoadSaveData);
			}
			if (saveData.ZoneSaveData != null)
			{
				ZoneGrid.Load(saveData.ZoneSaveData);
			}
		}

		public WorldStageSaveData Save()
		{
			return new WorldStageSaveData()
			{
				BuildingSaveData = GridData.Save(),
				RoadSaveData = RoadGraph.Save(),
				ZoneSaveData = ZoneGrid.Save()
			};
		}
	}
}