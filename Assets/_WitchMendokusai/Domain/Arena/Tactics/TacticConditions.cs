using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 전술 룰 조건 평가 — 순수(ICombatant + ITargetResolver + IsSkillReady 델리게이트). MonoBehaviour 0.
	/// EvalRule = 룰의 모든 조건 AND + 타겟 해석(out resolvedTarget). 타겟 필요 행동인데 타겟 없으면 발동 불가.
	/// </summary>
	public static class TacticConditions
	{
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
					// v1 미구현(아군 질의 필요) — 후속 구현 자리.
					return false;
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
