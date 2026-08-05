using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 전술 룰 조건 평가 — 순수(ICombatant + ITargetResolver + IsSkillReady 델리게이트). MonoBehaviour 0.
	/// EvalRule = 룰의 모든 조건 AND + 타겟 해석(out resolvedTarget). 타겟 필요 행동인데 타겟 없으면 발동 불가.
	/// </summary>
	public static class TacticConditions
	{
		// AllyCount 전용 질의 — 진영만 아군, 사거리 무제한(0), 우선순위는 세기만 하므로 무의미.
		private static readonly TargetQuery ALLY_HEADCOUNT =
			new(TargetSide.Ally, TargetPriority.Nearest, 0f);

		public static bool EvalRule(TacticRule rule, TacticContext context, out ICombatant resolvedTarget)
		{
			// 타겟 먼저 해석(조건·행동이 공유). Self 진영 기준 Query(사거리 필터 포함).
			resolvedTarget = context.Targeting.Query(context.Self, rule.Target);

			if (rule.Conditions != null)
			{
				foreach (TacticCondition condition in rule.Conditions)
				{
					if (EvalCondition(condition, context, resolvedTarget) == false)
						return false;
				}
			}

			if (NeedsTarget(rule.Action.Kind) && resolvedTarget == null)
				return false;

			return true;
		}

		private static bool NeedsTarget(ActionKind kind)
		{
			return kind switch
			{
				ActionKind.UseSkill => true,
				ActionKind.MoveToTarget => true,
				ActionKind.Approach => true,
				ActionKind.Retreat => true,
				_ => false,
			};
		}

		private static bool EvalCondition(TacticCondition condition, TacticContext context, ICombatant resolvedTarget)
		{
			switch (condition.Kind)
			{
				case ConditionKind.Always:
					return true;
				case ConditionKind.SelfHp:
					return Compare(context.Self.Hp, condition.Operator, condition.Value);
				case ConditionKind.SelfHpRatio:
					return Compare(HpRatio(context.Self), condition.Operator, condition.Value);
				case ConditionKind.TargetHpRatio:
					return resolvedTarget != null && Compare(HpRatio(resolvedTarget), condition.Operator, condition.Value);
				case ConditionKind.EnemyInRange:
					return resolvedTarget != null;
				case ConditionKind.SkillReady:
					return context.IsSkillReady(condition.SkillSlot);
				case ConditionKind.AllyCount:
					// 살아있는 아군 수(자기 제외 — 진영 필터가 뺀다). 사거리는 안 본다: 「몇 명 남았나」는
					// 판 전체의 사정이지 내 코앞의 사정이 아니다. 반경 안을 세고 싶어지면 그건 별개 조건이다.
					return Compare(
						context.Targeting.CountAlive(context.Self, ALLY_HEADCOUNT),
						condition.Operator, condition.Value);
				default:
					return false;
			}
		}

		private static float HpRatio(ICombatant combatant)
		{
			return combatant.HpMax > 0 ? (float)combatant.Hp / combatant.HpMax : 0f;
		}

		private static bool Compare(float lhs, ComparisonOperator op, float rhs)
		{
			return op switch
			{
				ComparisonOperator.Equal => Mathf.Approximately(lhs, rhs),
				ComparisonOperator.NotEqual => Mathf.Approximately(lhs, rhs) == false,
				ComparisonOperator.GreaterThan => lhs > rhs,
				ComparisonOperator.LessThan => lhs < rhs,
				ComparisonOperator.GreaterThanOrEqualTo => lhs >= rhs,
				ComparisonOperator.LessThanOrEqualTo => lhs <= rhs,
				_ => false,
			};
		}
	}
}
