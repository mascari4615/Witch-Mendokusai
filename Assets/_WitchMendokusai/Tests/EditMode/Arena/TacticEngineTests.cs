using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 전술 코어 풀파이프 회귀 — TacticProgram → 컴파일 → 우선순위 선택 → 조건평가 → 타겟해석 → 행동 디스패치.
	/// 전부 스텁(ICombatant/ITargetResolver/ITacticActuator)이라 MonoBehaviour/물리 0. WM-165 item 6.
	/// </summary>
	public class TacticEngineTests
	{
		private sealed class FakeCombatant : ICombatant
		{
			public int CombatantId { get; set; }
			public int TeamId { get; set; }
			public bool IsAlive { get; set; } = true;
			public Vector3 Position { get; set; }
			public int Hp { get; set; } = 100;
			public int HpMax { get; set; } = 100;
		}

		private sealed class FakeResolver : ITargetResolver
		{
			public ICombatant Result;
			public ICombatant Query(ICombatant self, TargetQuery query) => Result;
		}

		private sealed class RecordingActuator : ITacticActuator
		{
			public string LastAction = "none";
			public int LastSkillSlot = -1;
			public ICombatant LastTarget;

			public float LastStopDistance { get; private set; }

			public void UseSkill(int skillSlot, ICombatant target)
			{
				LastAction = "UseSkill";
				LastSkillSlot = skillSlot;
				LastTarget = target;
			}

			public void MoveToward(ICombatant target)
			{
				LastAction = "MoveToward";
				LastTarget = target;
			}

			public void Approach(ICombatant target, float stopDistance)
			{
				LastAction = "Approach";
				LastTarget = target;
				LastStopDistance = stopDistance;
			}

			public void Retreat(ICombatant target)
			{
				LastAction = "Retreat";
				LastTarget = target;
			}

			public void Hold()
			{
				LastAction = "Hold";
			}
		}

		private static TacticRule Rule(ActionKind action, int skillSlot, TargetPriority priority, params TacticCondition[] conditions)
		{
			return new TacticRule
			{
				Conditions = new List<TacticCondition>(conditions),
				Target = new TargetQuery(TargetSide.Enemy, priority),
				Action = new TacticAction { Kind = action, SkillSlot = skillSlot },
			};
		}

		private static TacticCondition Cond(ConditionKind kind, ComparisonOperator op, float value)
		{
			return new TacticCondition { Kind = kind, Operator = op, Value = value };
		}

		private static TacticCondition Always()
		{
			return new TacticCondition { Kind = ConditionKind.Always };
		}

		[Test]
		public void FirstMatchingRule_Executes_LowerRulesIgnored()
		{
			FakeCombatant self = new() { CombatantId = 0, TeamId = 0 };
			FakeCombatant enemy = new() { CombatantId = 1, TeamId = 1, Hp = 20, HpMax = 100 };
			FakeResolver resolver = new() { Result = enemy };
			RecordingActuator actuator = new();
			TacticContext context = new(self, resolver, actuator, slot => true);

			TacticProgram program = new();
			program.Rules.Add(Rule(ActionKind.UseSkill, 1, TargetPriority.LowestHpRatio, Cond(ConditionKind.TargetHpRatio, ComparisonOperator.LessThan, 0.3f)));
			program.Rules.Add(Rule(ActionKind.UseSkill, 0, TargetPriority.Nearest, Always()));

			TacticBTRunner runner = new(context, program);
			runner.UpdateBT();

			Assert.AreEqual("UseSkill", actuator.LastAction);
			Assert.AreEqual(1, actuator.LastSkillSlot, "처형 룰(슬롯1) 발동, fallback 무시");
			Assert.AreSame(enemy, actuator.LastTarget);
		}

		[Test]
		public void HighPriorityConditionFails_FallsToNextRule()
		{
			FakeCombatant self = new() { CombatantId = 0, TeamId = 0 };
			FakeCombatant enemy = new() { CombatantId = 1, TeamId = 1, Hp = 80, HpMax = 100 };
			FakeResolver resolver = new() { Result = enemy };
			RecordingActuator actuator = new();
			TacticContext context = new(self, resolver, actuator, slot => true);

			TacticProgram program = new();
			program.Rules.Add(Rule(ActionKind.UseSkill, 1, TargetPriority.LowestHpRatio, Cond(ConditionKind.TargetHpRatio, ComparisonOperator.LessThan, 0.3f)));
			program.Rules.Add(Rule(ActionKind.UseSkill, 0, TargetPriority.Nearest, Always()));

			TacticBTRunner runner = new(context, program);
			runner.UpdateBT();

			Assert.AreEqual(0, actuator.LastSkillSlot, "처형 조건 실패(HP 80%) → fallback 슬롯0");
		}

		[Test]
		public void SkillNotReady_RuleSkipped()
		{
			FakeCombatant self = new() { CombatantId = 0, TeamId = 0 };
			FakeCombatant enemy = new() { CombatantId = 1, TeamId = 1 };
			FakeResolver resolver = new() { Result = enemy };
			RecordingActuator actuator = new();
			TacticContext context = new(self, resolver, actuator, slot => false);

			TacticProgram program = new();
			program.Rules.Add(Rule(ActionKind.UseSkill, 1, TargetPriority.Nearest, Cond(ConditionKind.SkillReady, ComparisonOperator.Equal, 0f)));
			program.Rules.Add(Rule(ActionKind.Retreat, 0, TargetPriority.Nearest, Always()));

			TacticBTRunner runner = new(context, program);
			runner.UpdateBT();

			Assert.AreEqual("Retreat", actuator.LastAction, "스킬 쿨 → 룰 스킵 → fallback Retreat");
		}

		[Test]
		public void NoTarget_TargetedRuleSkipped()
		{
			FakeCombatant self = new() { CombatantId = 0, TeamId = 0 };
			FakeResolver resolver = new() { Result = null };
			RecordingActuator actuator = new();
			TacticContext context = new(self, resolver, actuator, slot => true);

			TacticProgram program = new();
			program.Rules.Add(Rule(ActionKind.UseSkill, 1, TargetPriority.Nearest, Always()));
			program.Rules.Add(Rule(ActionKind.Hold, 0, TargetPriority.Nearest, Always()));

			TacticBTRunner runner = new(context, program);
			runner.UpdateBT();

			Assert.AreEqual("Hold", actuator.LastAction, "타겟 없음 → UseSkill 룰 스킵 → Hold");
		}

		[Test]
		public void SelfHpRatioCondition_Gates()
		{
			FakeCombatant self = new() { CombatantId = 0, TeamId = 0, Hp = 40, HpMax = 100 };
			FakeCombatant enemy = new() { CombatantId = 1, TeamId = 1 };
			FakeResolver resolver = new() { Result = enemy };
			RecordingActuator actuator = new();
			TacticContext context = new(self, resolver, actuator, slot => true);

			TacticProgram program = new();
			program.Rules.Add(Rule(ActionKind.Retreat, 0, TargetPriority.Nearest, Cond(ConditionKind.SelfHpRatio, ComparisonOperator.LessThan, 0.5f)));
			program.Rules.Add(Rule(ActionKind.UseSkill, 0, TargetPriority.Nearest, Always()));

			TacticBTRunner runner = new(context, program);
			runner.UpdateBT();

			Assert.AreEqual("Retreat", actuator.LastAction, "내 HP 40% < 50% → 후퇴 룰");
		}

		[Test]
		public void HotSwapProgram_ChangesBehavior()
		{
			FakeCombatant self = new() { CombatantId = 0, TeamId = 0 };
			FakeCombatant enemy = new() { CombatantId = 1, TeamId = 1 };
			FakeResolver resolver = new() { Result = enemy };
			RecordingActuator actuator = new();
			TacticContext context = new(self, resolver, actuator, slot => true);

			TacticProgram attack = new();
			attack.Rules.Add(Rule(ActionKind.UseSkill, 2, TargetPriority.Nearest, Always()));

			TacticBTRunner runner = new(context, attack);
			runner.UpdateBT();
			Assert.AreEqual("UseSkill", actuator.LastAction);

			TacticProgram retreat = new();
			retreat.Rules.Add(Rule(ActionKind.Retreat, 0, TargetPriority.Nearest, Always()));
			runner.SetProgram(retreat);
			runner.UpdateBT();

			Assert.AreEqual("Retreat", actuator.LastAction, "SetProgram 재컴파일 → 행동 변화(핫스왑)");
		}
	}
}
