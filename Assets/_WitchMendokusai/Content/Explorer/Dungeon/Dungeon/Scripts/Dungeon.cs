using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public enum DungeonMapType
	{
		SurvivorsLike,
		Colosseum,
	}

	public enum DungeonObjectiveType
	{
		TimeSurvival,
		Domination,
		KillCount,
		Boss,
	}

	[Serializable]
	public struct DungeonSaveData
	{
		public Dictionary<int, bool> ConstraintSelected;

		public DungeonSaveData(Dictionary<int, bool> constraintSelected)
		{
			ConstraintSelected = constraintSelected;
		}
	}

	[CreateAssetMenu(fileName = nameof(Dungeon), menuName = "WM/Variable/Dungeon")]
	public class Dungeon : DataSO, ISavable<DungeonSaveData>
	{
		[field: Header("_" + nameof(Dungeon))]
		[PropertyOrder(100)][field: SerializeField] public DungeonObjectiveType ObjectiveType { get; private set; }
		[PropertyOrder(100)][field: SerializeField] public DungeonMapType MapType { get; private set; }
		[PropertyOrder(101)][field: SerializeField] public int ClearValue { get; private set; }
		[PropertyOrder(102)][field: SerializeField] public int TimeBySecond { get; private set; }
		[PropertyOrder(103)][field: SerializeField] public List<DungeonConstraint> Constraints { get; private set; }
		[PropertyOrder(104)][field: SerializeField] public List<Stage> Stages { get; private set; }
		[PropertyOrder(105)][field: SerializeField] public List<MonsterWave> MonsterWaves { get; set; }
		[PropertyOrder(106)][field: SerializeField] public List<ResourceNodeWave> ResourceNodeWaves { get; set; }
		[PropertyOrder(107)][field: SerializeField] public List<RewardInfo> Rewards { get; set; }

		[field: NonSerialized] public Dictionary<int, bool> ConstraintSelected { get; private set; } = new();

		public void Init()
		{
			ConstraintSelected = new();
			foreach (DungeonConstraint constraint in Constraints)
				ConstraintSelected.Add(constraint.ID, false);
		}

		public void Load(DungeonSaveData saveData)
		{
			// 현재 Constraints 기준으로 초기화 후, 세이브 데이터에 있는 값만 덮어쓰기
			// 새로 추가된 던전이나 Constraint가 세이브에 없어도 안전하게 동작
			Init();

			if (saveData.ConstraintSelected != null)
			{
				foreach (var kvp in saveData.ConstraintSelected)
				{
					if (ConstraintSelected.ContainsKey(kvp.Key))
						ConstraintSelected[kvp.Key] = kvp.Value;
				}
			}
		}

		public DungeonSaveData Save()
		{
			return new DungeonSaveData(ConstraintSelected);
		}
	}
}