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
		// 한 번이라도 깼는지. 옛 저장은 false (ClearedThreatCount 로 보충)
		public bool HasCleared;
		// 최고 클리어 위험도. 표시용. 옛 저장은 0
		public int BestRisk;
		// 받은 위험도 보상 구간의 위험도. 옛 저장은 null
		public List<int> ClaimedRiskMilestones;
		// 제약 해금 기록: 켜고 깬 제약 ID, 깬 인형 ID, 이 던전에서 잡은 보스 ID. 옛 저장은 null
		public List<int> ClearedConstraints;
		public List<int> ClearedDolls;
		public List<int> KilledBosses;
		// 세부 최고 기록 (순위표 원천). 옛 저장은 전부 0
		public DungeonBestRecords Bests;

		public DungeonSaveData(Dictionary<int, bool> constraintSelected, int clearedThreatCount, bool hasCleared, int bestRisk, List<int> claimedRiskMilestones, List<int> clearedConstraints, List<int> clearedDolls, List<int> killedBosses, DungeonBestRecords bests)
		{
			ConstraintSelected = constraintSelected;
			ClearedThreatCount = clearedThreatCount;
			HasCleared = hasCleared;
			BestRisk = bestRisk;
			ClaimedRiskMilestones = claimedRiskMilestones;
			ClearedConstraints = clearedConstraints;
			ClearedDolls = clearedDolls;
			KilledBosses = killedBosses;
			Bests = bests;
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

		public int CalculateDefeatNyang(int killCount) => Mathf.Min(DefeatNyangMax, Mathf.FloorToInt(killCount * DefeatNyangPerKill));

		[field: NonSerialized] public Dictionary<int, bool> ConstraintSelected { get; private set; } = new();
		[field: NonSerialized] public int ClearedThreatCount { get; private set; }
		[field: NonSerialized] public bool HasCleared { get; private set; }
		[field: NonSerialized] public int BestRisk { get; private set; }
		[field: NonSerialized] public DungeonBestRecords Bests { get; private set; }
		[field: NonSerialized] public HashSet<int> ClaimedRiskMilestones { get; private set; } = new();
		[field: NonSerialized] public HashSet<int> ClearedConstraints { get; private set; } = new();
		[field: NonSerialized] public HashSet<int> ClearedDolls { get; private set; } = new();
		[field: NonSerialized] public HashSet<int> KilledBosses { get; private set; } = new();
		// 해금된 제약 ID. RefreshConstraintUnlocks 가 채움 (조건 판정에 CriteriaContext 가 필요해 저장값이 아니라 갱신값)
		[field: NonSerialized] public HashSet<int> UnlockedConstraints { get; private set; } = new();

		// 위협 단계 승리 기록이 있으면 클리어한 것 (HasCleared 이전 저장)
		public bool IsCleared => HasCleared || ClearedThreatCount > 0;
		public bool IsConstraintUnlocked(DungeonConstraint constraint) => UnlockedConstraints.Contains(constraint.ID);

		// 입장 창에서 켠 제약 중 해금된 것만 판에 적용
		public List<DungeonConstraint> SelectedConstraints() => Constraints.FindAll(constraint => ConstraintSelected[constraint.ID] && IsConstraintUnlocked(constraint));

		/// <summary> 해금 다시 판정. 잠긴 제약은 켬 상태도 끔 (보이지 않는 제약이 다시 하기에 적용되지 않게). 입장 창 열 때와 판 시작 때 </summary>
		public void RefreshConstraintUnlocks(CriteriaContext context)
		{
			UnlockedConstraints.Clear();
			foreach (DungeonConstraint constraint in Constraints)
			{
				if (DungeonConstraintUnlockRules.IsUnlocked(this, constraint, context))
					UnlockedConstraints.Add(constraint.ID);
				else
					ConstraintSelected[constraint.ID] = false;
			}
		}

		public void RecordBossKill(int bossId) => KilledBosses.Add(bossId);

		/// <summary> 판 결과로 세부 최고 기록 갱신. 패배한 판도 부름 </summary>
		public void RecordRunResult(DungeonRecord result) => Bests = DungeonRecordRules.Merge(Bests, result);

		/// <summary> 세부 최고 기록과 최고 위험도를 기록판 (GameRecords) 에 알림. 세이브 로드 뒤, 판 끝 </summary>
		public void ReportRecords()
		{
			foreach ((DungeonRecordStat stat, int value) in DungeonRecordRules.Values(Bests, BestRisk))
				GameRecords.Report(DungeonRecordRules.RecordKey(ID, stat), value);
		}

		public void RecordThreatClear(int level) => ClearedThreatCount = DungeonThreatRules.AfterClear(ClearedThreatCount, level);

		/// <summary> 클리어 기록. 이번 위험도로 처음 닿은 보상 구간을 돌려줌 (지급은 부르는 쪽) </summary>
		public List<DungeonConstraintRewardRule.RiskMilestone> RecordClear(IReadOnlyList<DungeonConstraint> applied, int dollId)
		{
			int risk = DungeonConstraintRules.TotalRisk(applied);
			foreach (DungeonConstraint constraint in applied)
				ClearedConstraints.Add(constraint.ID);
			ClearedDolls.Add(dollId);
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
			Bests = default;
			ClaimedRiskMilestones = new();
			ClearedConstraints = new();
			ClearedDolls = new();
			KilledBosses = new();
			UnlockedConstraints = new();
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
			Bests = saveData.Bests;
			if (saveData.ClaimedRiskMilestones != null)
				ClaimedRiskMilestones.UnionWith(saveData.ClaimedRiskMilestones);
			if (saveData.ClearedConstraints != null)
				ClearedConstraints.UnionWith(saveData.ClearedConstraints);
			if (saveData.ClearedDolls != null)
				ClearedDolls.UnionWith(saveData.ClearedDolls);
			if (saveData.KilledBosses != null)
				KilledBosses.UnionWith(saveData.KilledBosses);

			if (saveData.ConstraintSelected != null)
			{
				foreach (KeyValuePair<int, bool> kvp in saveData.ConstraintSelected)
				{
					if (ConstraintSelected.ContainsKey(kvp.Key))
						ConstraintSelected[kvp.Key] = kvp.Value;
				}
			}
			DungeonConstraintRules.Normalize(this, ConstraintSelected);
		}

		public DungeonSaveData Save()
		{
			return new DungeonSaveData(ConstraintSelected, ClearedThreatCount, HasCleared, BestRisk, new List<int>(ClaimedRiskMilestones),
				new List<int>(ClearedConstraints), new List<int>(ClearedDolls), new List<int>(KilledBosses), Bests);
		}
	}
}