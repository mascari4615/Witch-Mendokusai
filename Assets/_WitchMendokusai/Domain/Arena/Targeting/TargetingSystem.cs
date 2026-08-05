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

		// 진영별 목표물(TD 코어 / 넥서스 / 사령부). TargetSide.EnemyObjective 질의가 이 집합만 후보로 삼는다.
		// 일반 전투 참가자이기도 하므로 combatants 에도 함께 등록됨(Register 와 직교) — 여기 등록은 "목표물 표시" 뿐.
		private readonly List<ICombatant> objectives = new();

		public void Register(ICombatant combatant)
		{
			if (combatants.Contains(combatant) == false)
				combatants.Add(combatant);
		}

		public void Unregister(ICombatant combatant)
		{
			combatants.Remove(combatant);
			objectives.Remove(combatant);
		}

		/// <summary> 이 전투 참가자를 소속 팀의 목표물로 표시. Register 와 별개(둘 다 호출해야 일반 타겟 + 목표물 양쪽으로 잡힘). </summary>
		public void RegisterObjective(ICombatant objective)
		{
			if (objectives.Contains(objective) == false)
				objectives.Add(objective);
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

				bool better;
				if (best == null)
					better = true;
				else if (ScoresTied(score, bestScore))
					better = candidate.CombatantId < bestId; // 동점 → 등록 순서가 아니라 id 가 가른다.
				else
					better = score < bestScore;

				if (better)
				{
					best = candidate;
					bestScore = score;
					bestId = candidate.CombatantId;
				}
			}

			return best;
		}

		/// <summary>
		/// 대칭 배치에서 「똑같이 먼」 둘은 수학적으론 동점인데 float 로는 마지막 자리가 갈린다
		/// (거리 제곱은 곱셈·뺄셈을 거친다). 그 1 ULP 차이에 <c>score == bestScore</c> 가 false 를
		/// 돌려주면 <b>id 타이브레이크가 통째로 건너뛰어지고, 승자를 부동소수 잡음이 정한다.</b>
		///
		/// 한 기계 안에서는 그래도 같은 답이 나와 조용하다. 깨지는 자리는 *다른 기계*다 —
		/// 잡음의 마지막 자리는 플랫폼·JIT 마다 달라서, 같은 입력으로 두 피어가 다른 타겟을 고른다.
		/// 결정성이 곧 lockstep 의 전제라(TASK-WM-085) 지금 좁은 띠로 「같음」을 정의해 둔다.
		///
		/// 띠는 <b>상대값</b>이다: 같은 코드가 거리 제곱(수백)·HP(정수)·HP 비율(0~1)을 다 다룬다.
		/// 절대 epsilon 하나로는 한쪽에서 너무 넓고 다른 쪽에선 무의미해진다.
		/// (밸런스 수치가 아니라 부동소수 정밀도 상수 — 인스펙터로 낼 대상이 아니다.)
		///
		/// 한계 정직: 띠 비교는 추이적이지 않다(a≈b, b≈c 인데 a≉c 가 가능). 한 매치 후보가 한 자리
		/// 수라 실질 영향은 없고, 진짜 크로스플랫폼 lockstep 까지 가면 고정소수 양자화가 정답이다.
		/// </summary>
		private const float SCORE_TIE_BAND = 1e-5f;

		private static bool ScoresTied(float a, float b)
		{
			float scale = Mathf.Max(1f, Mathf.Max(Mathf.Abs(a), Mathf.Abs(b)));
			return Mathf.Abs(a - b) <= SCORE_TIE_BAND * scale;
		}

		private bool PassesFilter(ICombatant self, ICombatant candidate, TargetQuery query)
		{
			if (candidate.IsAlive == false)
				return false;

			bool sideOk = query.Side switch
			{
				TargetSide.Enemy => candidate.TeamId != self.TeamId,
				TargetSide.Ally => candidate.TeamId == self.TeamId && candidate != self,
				TargetSide.Self => candidate == self,
				// 적 진영 + 목표물로 표시된 것만. 표시 안 됐으면 후보 0 → 질의 null (전진 룰이 fallthrough).
				TargetSide.EnemyObjective => candidate.TeamId != self.TeamId && objectives.Contains(candidate),
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
