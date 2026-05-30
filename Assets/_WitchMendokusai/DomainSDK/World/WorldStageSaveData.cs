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
	}
}
