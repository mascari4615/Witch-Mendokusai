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
		[PropertyOrder(106)][field: SerializeField] public List<RewardInfo> Rewards { get; set; }

		[field: NonSerialized] public Dictionary<int, bool> ConstraintSelected { get; private set; } = new();

		public void Init()
		{
			ConstraintSelected = new();
			foreach (DungeonConstraint constraint in Constraints)
				ConstraintSelected.Add(constraint.ID, false);
		}

		public void Load(DungeonSaveData saveData)
		{
			ConstraintSelected = saveData.ConstraintSelected;
		}

		public DungeonSaveData Save()
		{
			return new DungeonSaveData(ConstraintSelected);
		}
	}
}