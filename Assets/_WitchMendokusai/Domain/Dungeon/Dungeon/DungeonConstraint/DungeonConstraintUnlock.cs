using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	public enum DungeonConstraintUnlockType
	{
		// 이 던전 첫 클리어
		FirstClear = 0,
		// 같은 묶음 바로 아래 단계를 켜고 이 던전 클리어
		LowerLevel = 1,
		// 이 던전을 위험도 Value 이상으로 클리어
		RiskClear = 2,
		// Target 보스 처치 (던전 무관)
		BossKill = 3,
		// Target 인형으로 이 던전 클리어
		DollClear = 4,
		// 기존 조건 체계 (CriteriaInfo). 누적 처치, 업적 자리
		Criteria = 5,
		// Target 제약을 켜고 이 던전 클리어 (다른 묶음)
		ConstraintClear = 6,
	}

	/// <summary> 제약 하나의 해금 조건. 제약의 조건 목록은 전부 채워야 열림, 비면 처음부터 열림 </summary>
	[Serializable]
	public struct DungeonConstraintUnlock
	{
		public DungeonConstraintUnlockType Type;
		[Tooltip("BossKill: 보스 몬스터, DollClear: 인형")]
		public DataSO Target;
		[Tooltip("RiskClear: 위험도")]
		public int Value;
		[Tooltip("Criteria: 조건 (예: 게임 통계 누적 처치 >= 1000)")]
		public CriteriaInfo Criteria;
	}

	/// <summary> 제약 해금 판정과 조건 문장. 기록은 Dungeon 저장 (클리어, 최고 위험도, 켜고 깬 제약, 깬 인형, 잡은 보스) </summary>
	public static class DungeonConstraintUnlockRules
	{
		public static bool IsUnlocked(Dungeon dungeon, DungeonConstraint constraint, CriteriaContext context)
		{
			foreach (DungeonConstraintUnlock unlock in constraint.Unlocks)
			{
				if (IsMet(dungeon, constraint, unlock, context) == false)
					return false;
			}
			return true;
		}

		public static bool IsMet(Dungeon dungeon, DungeonConstraint constraint, DungeonConstraintUnlock unlock, CriteriaContext context) => unlock.Type switch
		{
			DungeonConstraintUnlockType.FirstClear => dungeon.IsCleared,
			DungeonConstraintUnlockType.LowerLevel => LowerLevel(dungeon, constraint).Exists(lower => dungeon.ClearedConstraints.Contains(lower.ID)),
			DungeonConstraintUnlockType.RiskClear => dungeon.IsCleared && dungeon.BestRisk >= unlock.Value,
			DungeonConstraintUnlockType.BossKill => AnyDungeonKilled(context, unlock.Target.ID),
			DungeonConstraintUnlockType.DollClear => dungeon.ClearedDolls.Contains(unlock.Target.ID),
			DungeonConstraintUnlockType.Criteria => CriteriaFactory.Create(unlock.Criteria, context).Evaluate(),
			DungeonConstraintUnlockType.ConstraintClear => dungeon.ClearedConstraints.Contains(unlock.Target.ID),
			_ => throw new ArgumentOutOfRangeException(nameof(unlock), unlock.Type, "처리하지 않는 해금 조건"),
		};

		/// <summary> 잠긴 칸 설명. 못 채운 조건만 이어 붙임 </summary>
		public static string Describe(Dungeon dungeon, DungeonConstraint constraint, CriteriaContext context)
		{
			List<string> parts = new();
			foreach (DungeonConstraintUnlock unlock in constraint.Unlocks)
			{
				if (IsMet(dungeon, constraint, unlock, context) == false)
					parts.Add(Text(constraint, unlock, context));
			}
			return string.Join(", ", parts);
		}

		private static string Text(DungeonConstraint constraint, DungeonConstraintUnlock unlock, CriteriaContext context) => unlock.Type switch
		{
			DungeonConstraintUnlockType.FirstClear => "이 던전 첫 클리어",
			DungeonConstraintUnlockType.LowerLevel => $"{constraint.Name} {constraint.Level - 1}단계를 켜고 클리어",
			DungeonConstraintUnlockType.RiskClear => $"위험도 {unlock.Value} 이상으로 클리어",
			DungeonConstraintUnlockType.BossKill => $"{unlock.Target.Name} 처치",
			DungeonConstraintUnlockType.DollClear => $"{unlock.Target.Name} 인형으로 클리어",
			DungeonConstraintUnlockType.Criteria => CriteriaText(unlock.Criteria, context),
			DungeonConstraintUnlockType.ConstraintClear => $"{unlock.Target.Name} {(unlock.Target as DungeonConstraint).Level}단계를 켜고 클리어",
			_ => throw new ArgumentOutOfRangeException(nameof(unlock), unlock.Type, "처리하지 않는 해금 조건"),
		};

		private static string CriteriaText(CriteriaInfo criteriaInfo, CriteriaContext context)
		{
			Criteria criteria = CriteriaFactory.Create(criteriaInfo, context);
			string name = criteriaInfo.Data != null ? criteriaInfo.Data.Name : criteriaInfo.Type.ToString();
			return criteria is NumCriteria number ? $"{name} {number.GetCurValue()}/{number.TargetValue}" : name;
		}

		private static List<DungeonConstraint> LowerLevel(Dungeon dungeon, DungeonConstraint constraint) =>
			dungeon.Constraints.FindAll(candidate => candidate.FamilyKey == constraint.FamilyKey && candidate.Level == constraint.Level - 1);

		private static bool AnyDungeonKilled(CriteriaContext context, int bossId)
		{
			foreach (DataSO data in context.SOManager.DataSOs[typeof(Dungeon)].Values)
			{
				if ((data as Dungeon).KilledBosses.Contains(bossId))
					return true;
			}
			return false;
		}
	}
}
