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

	// Json.NET 전용 저장 DTO — Unity 직렬화 대상이 아니라 [Serializable] 을 달지 않는다(GameData 주석 참조).
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
		[Tooltip("패배 보상: 생존 1분당 냥")]
		[PropertyOrder(108)][field: SerializeField, Min(0f)] public float DefeatNyangPerMinute { get; private set; } = 20f;
		[Tooltip("패배 보상: 처치 1마리당 냥")]
		[PropertyOrder(109)][field: SerializeField, Min(0f)] public float DefeatNyangPerKill { get; private set; } = 0.05f;
		[Tooltip("패배 보상 상한")]
		[PropertyOrder(110)][field: SerializeField, Min(0)] public int DefeatNyangMax { get; private set; } = 300;

		public int CalculateDefeatNyang(TimeSpan playTime, int killCount)
		{
			float raw = (float)playTime.TotalMinutes * DefeatNyangPerMinute + killCount * DefeatNyangPerKill;
			return Mathf.Min(DefeatNyangMax, Mathf.FloorToInt(raw));
		}

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
				foreach (KeyValuePair<int, bool> kvp in saveData.ConstraintSelected)
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