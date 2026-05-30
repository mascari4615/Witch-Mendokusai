using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 매치 스코프 타겟 선정. 살아있는 ICombatant 레지스트리에서 TargetQuery 에 맞는 단일 타겟 반환.
	/// 정렬 = 우선순위 점수(작을수록 우선) + CombatantId 타이브레이크 → 결정적(리플레이/후속 lockstep 정합).
	/// 3v3 = 최대 6기라 O(n) 단순 비교(공간 분할 불요). ITargetResolver 구현이라 EditMode 스텁 테스트 가능.
	/// </summary>
	public class TargetingSystem : ITargetResolver
	{
		private readonly List<ICombatant> combatants = new();

		public void Register(ICombatant combatant)
		{
			if (combatants.Contains(combatant) == false)
				combatants.Add(combatant);
		}

		public void Unregister(ICombatant combatant)
		{
			combatants.Remove(combatant);
		}

		public ICombatant Query(ICombatant self, TargetQuery query)
		{
			ICombatant best = null;
			float bestScore = 0f;
			int bestId = 0;

			foreach (ICombatant candidate in combatants)
			{
				if (PassesFilter(self, candidate, query) == false)
					continue;

				// 점수는 "작을수록 우선" 으로 정규화 → 단일 비교로 모든 우선순위 처리.
				float score = ScoreOf(self, candidate, query.Priority);

				bool better = best == null
					|| score < bestScore
					|| (score == bestScore && candidate.CombatantId < bestId);
				if (better)
				{
					best = candidate;
					bestScore = score;
					bestId = candidate.CombatantId;
				}
			}

			return best;
		}

		private static bool PassesFilter(ICombatant self, ICombatant candidate, TargetQuery query)
		{
			if (candidate.IsAlive == false)
				return false;

			bool sideOk = query.Side switch
			{
				TargetSide.Enemy => candidate.TeamId != self.TeamId,
				TargetSide.Ally => candidate.TeamId == self.TeamId && candidate != self,
				TargetSide.Self => candidate == self,
				_ => false,
			};
			if (sideOk == false)
				return false;

			if (query.MaxRange > 0f)
			{
				float sqrRange = query.MaxRange * query.MaxRange;
				if ((candidate.Position - self.Position).sqrMagnitude > sqrRange)
					return false;
			}

			return true;
		}

		private static float ScoreOf(ICombatant self, ICombatant candidate, TargetPriority priority)
		{
			return priority switch
			{
				TargetPriority.Nearest => (candidate.Position - self.Position).sqrMagnitude,
				TargetPriority.Farthest => -(candidate.Position - self.Position).sqrMagnitude,
				TargetPriority.LowestHp => candidate.Hp,
				TargetPriority.HighestHp => -candidate.Hp,
				TargetPriority.LowestHpRatio => HpRatio(candidate),
				TargetPriority.HighestHpRatio => -HpRatio(candidate),
				_ => 0f,
			};
		}

		private static float HpRatio(ICombatant combatant)
		{
			return combatant.HpMax > 0 ? (float)combatant.Hp / combatant.HpMax : 0f;
		}
	}
}
