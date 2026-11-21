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
		[Tooltip("Criteria: 화면 문장 (예: 몬스터 처치). 통계 이름은 내부 상수라 따로 씀")]
		public string Label;
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
			DungeonConstraintUnlockType.LowerLevel => LowerLevelCleared(dungeon, constraint),
			DungeonConstraintUnlockType.RiskClear => dungeon.IsCleared && dungeon.BestRisk >= unlock.Value,
			DungeonConstraintUnlockType.BossKill => AnyDungeonKilled(context, ((Monster)unlock.Target).ID),
			DungeonConstraintUnlockType.DollClear => dungeon.ClearedDolls.Contains(((Doll)unlock.Target).ID),
			DungeonConstraintUnlockType.Criteria => CriteriaFactory.Create(unlock.Criteria, context).Evaluate(),
			DungeonConstraintUnlockType.ConstraintClear => dungeon.ClearedConstraints.Contains(((DungeonConstraint)unlock.Target).ID),
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
			DungeonConstraintUnlockType.Criteria => CriteriaText(unlock, context),
			DungeonConstraintUnlockType.ConstraintClear => $"{unlock.Target.Name} {((DungeonConstraint)unlock.Target).Level}단계를 켜고 클리어",
			_ => throw new ArgumentOutOfRangeException(nameof(unlock), unlock.Type, "처리하지 않는 해금 조건"),
		};

		// 문장이 비면 데이터 잘못 (통계 이름은 내부 상수라 화면에 못 씀)
		private static string CriteriaText(DungeonConstraintUnlock unlock, CriteriaContext context)
		{
			if (string.IsNullOrEmpty(unlock.Label))
				throw new InvalidOperationException("Criteria 해금 조건에 Label 이 없음");
			Criteria criteria = CriteriaFactory.Create(unlock.Criteria, context);
			return criteria is NumCriteria number ? $"{unlock.Label} {number.GetCurValue()}/{number.TargetValue}" : unlock.Label;
		}

		// 단계 1 에 아래 단계 조건, 또는 아래 단계 에셋이 없으면 영원히 잠기므로 데이터 잘못
		private static bool LowerLevelCleared(Dungeon dungeon, DungeonConstraint constraint)
		{
			List<DungeonConstraint> lower = dungeon.Constraints.FindAll(candidate => candidate.FamilyKey == constraint.FamilyKey && candidate.Level == constraint.Level - 1);
			if (lower.Count == 0)
				throw new InvalidOperationException($"{constraint.name}: 아래 단계 조건인데 {dungeon.name} 에 단계 {constraint.Level - 1} 이 없음");
			return lower.Exists(item => dungeon.ClearedConstraints.Contains(item.ID));
		}

		private static bool AnyDungeonKilled(CriteriaContext context, int bossId)
		{
			foreach (DataSO data in context.SOManager.DataSOs[typeof(Dungeon)].Values)
			{
				if (((Dungeon)data).KilledBosses.Contains(bossId))
					return true;
			}
			return false;
		}
	}
}
