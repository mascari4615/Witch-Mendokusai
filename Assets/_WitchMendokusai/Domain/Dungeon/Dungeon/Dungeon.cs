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
		// 아래부터 승리한 위협 단계 수, 최고 승리 단계 + 1. 옛 저장은 0
		public int ClearedThreatCount;
		// 한 번이라도 깼는지. 제약 설정 해금 조건. 옛 저장은 false (ClearedThreatCount 로 보충)
		public bool HasCleared;
		// 최고 클리어 위험도. 표시용. 옛 저장은 0
		public int BestRisk;
		// 받은 위험도 보상 구간의 위험도. 옛 저장은 null
		public List<int> ClaimedRiskMilestones;

		public DungeonSaveData(Dictionary<int, bool> constraintSelected, int clearedThreatCount, bool hasCleared, int bestRisk, List<int> claimedRiskMilestones)
		{
			ConstraintSelected = constraintSelected;
			ClearedThreatCount = clearedThreatCount;
			HasCleared = hasCleared;
			BestRisk = bestRisk;
			ClaimedRiskMilestones = claimedRiskMilestones;
		}
	}

	[CreateAssetMenu(fileName = nameof(Dungeon), menuName = "WM/Variable/Dungeon")]
	public partial class Dungeon : DataSO, ISavable<DungeonSaveData>
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
		[PropertyOrder(108)][field: SerializeField] public SurvivalEventTable EventTable { get; private set; }
		[Tooltip("패배 보상: 처치 1마리당 냥")]
		[PropertyOrder(109)][field: SerializeField, Min(0f)] public float DefeatNyangPerKill { get; private set; } = 0.05f;
		[Tooltip("패배 보상 상한")]
		[PropertyOrder(110)][field: SerializeField, Min(0)] public int DefeatNyangMax { get; private set; } = 300;

		[Tooltip("위협 단계 선택 가능 여부. 표는 SurvivalRunRules.ThreatLevels")]
		[PropertyOrder(111)][field: SerializeField] public bool HasThreatLevels { get; private set; }

		[Tooltip("개발 빌드에서만 입장 목록에 표시. 데모 릴리스에서 실험 모드 숨김")]
		[PropertyOrder(112)][field: SerializeField] public bool DevBuildOnly { get; private set; }

		[Tooltip("입장 화면 대표 그림. 비우면 테마 기본 그림")]
		[PropertyOrder(113)][field: SerializeField] public Sprite KeyArt { get; private set; }

		[Tooltip("위험도 단계 첫 클리어 보상. 비우면 보상 없는 순수 도전 (최고 기록만)")]
		[PropertyOrder(114)][field: SerializeField] public DungeonConstraintRewardRule ConstraintRewardRule { get; private set; }

		[Tooltip("켜면 첫 클리어 전에는 제약 설정이 잠기고 제약이 적용되지 않음")]
		[PropertyOrder(115)][field: SerializeField] public bool ConstraintsNeedFirstClear { get; private set; } = true;

		public int CalculateDefeatNyang(int killCount) => Mathf.Min(DefeatNyangMax, Mathf.FloorToInt(killCount * DefeatNyangPerKill));

		[field: NonSerialized] public Dictionary<int, bool> ConstraintSelected { get; private set; } = new();
		[field: NonSerialized] public int ClearedThreatCount { get; private set; }
		[field: NonSerialized] public bool HasCleared { get; private set; }
		[field: NonSerialized] public int BestRisk { get; private set; }
		[field: NonSerialized] public HashSet<int> ClaimedRiskMilestones { get; private set; } = new();

		// 위협 단계 승리 기록이 있으면 클리어한 것 (HasCleared 이전 저장)
		public bool IsCleared => HasCleared || ClearedThreatCount > 0;
		public bool ConstraintsUnlocked => ConstraintsNeedFirstClear == false || IsCleared;

		// 입장 창에서 켠 제약만 판에 적용. 잠긴 동안은 없음
		public List<DungeonConstraint> SelectedConstraints() => ConstraintsUnlocked ? Constraints.FindAll(constraint => ConstraintSelected[constraint.ID]) : new List<DungeonConstraint>();

		public void RecordThreatClear(int level) => ClearedThreatCount = DungeonThreatRules.AfterClear(ClearedThreatCount, level);

		/// <summary> 클리어 기록. 이번 위험도로 처음 닿은 보상 구간을 돌려줌 (지급은 부르는 쪽) </summary>
		public List<DungeonConstraintRewardRule.RiskMilestone> RecordClear(int risk)
		{
			List<DungeonConstraintRewardRule.RiskMilestone> reached = new();
			if (ConstraintRewardRule != null)
			{
				foreach (int index in DungeonRiskRewardRules.NewlyReached(ConstraintRewardRule.MilestoneRisks(), ClaimedRiskMilestones, risk))
					reached.Add(ConstraintRewardRule.Milestones[index]);
			}
			foreach (DungeonConstraintRewardRule.RiskMilestone milestone in reached)
				ClaimedRiskMilestones.Add(milestone.Risk);
			HasCleared = true;
			BestRisk = DungeonRiskRewardRules.AfterClear(BestRisk, risk);
			return reached;
		}

		public void Init()
		{
			ClearedThreatCount = 0;
			HasCleared = false;
			BestRisk = 0;
			ClaimedRiskMilestones = new();
			ConstraintSelected = new();
			foreach (DungeonConstraint constraint in Constraints)
				ConstraintSelected.Add(constraint.ID, false);
		}

		public void Load(DungeonSaveData saveData)
		{
			// 현재 Constraints 기준으로 초기화 후, 세이브 데이터에 있는 값만 덮어쓰기
			// 새로 추가된 던전이나 Constraint가 세이브에 없어도 안전하게 동작
			Init();
			ClearedThreatCount = saveData.ClearedThreatCount;
			HasCleared = saveData.HasCleared;
			BestRisk = saveData.BestRisk;
			if (saveData.ClaimedRiskMilestones != null)
				ClaimedRiskMilestones.UnionWith(saveData.ClaimedRiskMilestones);

			if (saveData.ConstraintSelected != null)
			{
				foreach (KeyValuePair<int, bool> kvp in saveData.ConstraintSelected)
				{
					if (ConstraintSelected.ContainsKey(kvp.Key))
						ConstraintSelected[kvp.Key] = kvp.Value;
				}
			}
			DungeonConstraintRules.Normalize(this, ConstraintSelected);
			// 잠긴 던전에 옛 저장의 켠 제약이 남으면 첫 클리어 직후 다시 하기에서 보이지 않게 적용됨. 잠긴 동안은 전부 끔
			if (ConstraintsUnlocked == false)
			{
				foreach (DungeonConstraint constraint in Constraints)
					ConstraintSelected[constraint.ID] = false;
			}
		}

		public DungeonSaveData Save()
		{
			return new DungeonSaveData(ConstraintSelected, ClearedThreatCount, HasCleared, BestRisk, new List<int>(ClaimedRiskMilestones));
		}
	}
}