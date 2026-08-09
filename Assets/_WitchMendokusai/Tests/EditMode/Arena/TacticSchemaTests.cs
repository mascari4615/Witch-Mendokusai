using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
// ★ 좌표는 판정 쪽 (TASK-WM-214) — 시험이 구현하는 ICombatant 가 판정 타입을 쓴다.
using Vector3 = WitchMendokusai.Numerics.Vector3;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// TacticSchema 가 <b>실제 평가 동작과 일치하는지</b>를 기계로 대조한다.
	///
	/// 표를 손으로 베껴 적는 시험은 의미가 없다 — 표가 틀렸을 때 같이 틀리기 때문이다.
	/// 그래서 여기선 「Operator/Value 를 바꿨을 때 평가 결과가 달라지는가」를 <b>관찰</b>해서
	/// 그 관찰값과 TacticSchema 의 대답을 비교한다. 새 ConditionKind 를 append 하고
	/// 스키마를 안 고치면 이 시험이 즉시 깨진다 — 편집기에 칸이 안 생기는 사고를 컴파일 다음 줄에서 잡는다.
	///
	/// 배경: 편집기가 Operator/Value 칸을 안 만들던 시절, 수치 조건은 전부 기본값
	/// (Equal, 0) 으로 굳어 「HP 비율 == 0」= 죽어야 참이 됐다. 영영 발동하지 않는 줄인데
	/// 화면에선 그냥 「그 줄이 안 먹네」로만 보인다.
	/// </summary>
	public class TacticSchemaTests
	{
		private sealed class FakeCombatant : ICombatant
		{
			public int CombatantId { get; set; }
			public int TeamId { get; set; }
			public bool IsAlive { get; set; } = true;
			public Vector3 Position { get; set; }
			public int Hp { get; set; } = 50;
			public int HpMax { get; set; } = 100;
		}

		private sealed class FakeResolver : ITargetResolver
		{
			public ICombatant Result;
			public int AliveCount = 3;
			public ICombatant Query(ICombatant self, TargetQuery query) => Result;
			public int CountAlive(ICombatant self, TargetQuery query) => AliveCount;
		}

		// 조건 평가만 관찰하므로 행동은 전부 무시한다.
		private sealed class NullActuator : ITacticActuator
		{
			public bool StopsToAttack => true;
			public void UseSkill(int skillSlot, ICombatant target) { }
			public void MoveToward(ICombatant target) { }
			public void Approach(ICombatant target, float stopDistance) { }
			public void Retreat(ICombatant target) { }
			public void Hold() { }
		}

		// Hp=50/100 → SelfHp=50, SelfHpRatio=0.5, TargetHpRatio=0.5, AllyCount=3.
		// 전부 -999 보다 크다 → GreaterThan 이면 참, LessThan 이면 거짓. 즉 Operator 를 읽는
		// 조건이면 반드시 결과가 뒤집힌다. 안 읽으면 두 번 다 같은 값이 나온다.
		private const float FAR_BELOW_ANY_VALUE = -999f;

		private static bool Eval(ConditionKind kind, ComparisonOperator op, float value, int skillSlot, Func<int, bool> isSkillReady)
		{
			FakeCombatant self = new();
			FakeResolver resolver = new() { Result = new FakeCombatant() };
			TacticContext context = new(self, resolver, new NullActuator(), isSkillReady);

			TacticRule rule = new()
			{
				Conditions = new List<TacticCondition>
				{
					new() { Kind = kind, Operator = op, Value = value, SkillSlot = skillSlot },
				},
				Target = new TargetQuery(TargetSide.Enemy, TargetPriority.Nearest, 0f),
				// Hold = 타겟이 필요 없는 유일한 행동. 타겟 유무가 조건 관찰을 오염시키지 않게.
				Action = new TacticAction { Kind = ActionKind.Hold },
			};

			return TacticConditions.EvalRule(rule, context, out _);
		}

		[Test]
		public void UsesThreshold_가_실제_평가_동작과_일치한다()
		{
			foreach (ConditionKind kind in (ConditionKind[])Enum.GetValues(typeof(ConditionKind)))
			{
				bool withGreater = Eval(kind, ComparisonOperator.GreaterThan, FAR_BELOW_ANY_VALUE, 0, _ => true);
				bool withLess = Eval(kind, ComparisonOperator.LessThan, FAR_BELOW_ANY_VALUE, 0, _ => true);
				bool observed = withGreater != withLess;

				Assert.AreEqual(TacticSchema.UsesThreshold(kind), observed,
					$"{kind}: TacticSchema.UsesThreshold 와 실제 평가가 어긋난다. " +
					"스키마가 false 인데 실제로 Operator 를 읽으면 편집기에 칸이 안 생겨 기본값으로 굳는다.");
			}
		}

		[Test]
		public void UsesSkillSlot_조건_이_실제_평가_동작과_일치한다()
		{
			// 0번 슬롯만 준비됨 → SkillSlot 을 읽는 조건이면 0 과 1 의 결과가 갈린다.
			Func<int, bool> onlySlotZeroReady = slot => slot == 0;

			foreach (ConditionKind kind in (ConditionKind[])Enum.GetValues(typeof(ConditionKind)))
			{
				bool withSlotZero = Eval(kind, ComparisonOperator.GreaterThan, FAR_BELOW_ANY_VALUE, 0, onlySlotZeroReady);
				bool withSlotOne = Eval(kind, ComparisonOperator.GreaterThan, FAR_BELOW_ANY_VALUE, 1, onlySlotZeroReady);
				bool observed = withSlotZero != withSlotOne;

				Assert.AreEqual(TacticSchema.UsesSkillSlot(kind), observed, $"{kind}: SkillSlot 사용 여부가 어긋난다.");
			}
		}

		// 행동 쪽은 EvalRule 이 아니라 디스패치에서 읽는다(ExecAction). 그쪽 회귀는
		// TacticEngineTests 가 LastSkillSlot 으로 이미 잡고 있어 여기선 계약만 못박는다.
		[Test]
		public void UsesSkillSlot_행동_은_UseSkill_뿐이다()
		{
			foreach (ActionKind kind in (ActionKind[])Enum.GetValues(typeof(ActionKind)))
				Assert.AreEqual(kind == ActionKind.UseSkill, TacticSchema.UsesSkillSlot(kind), kind.ToString());
		}

		// 편집기가 「칸을 하나도 안 보여줘도 되는」 조건은 불리언형뿐이다. 수치형이 하나라도
		// 칸 없이 남으면 그 줄은 (Equal, 0) 으로 굳는다 — 이 시험이 그 상태를 못 지나가게 한다.
		[Test]
		public void 수치형_조건이_최소_하나는_있다()
		{
			int numeric = 0;
			foreach (ConditionKind kind in (ConditionKind[])Enum.GetValues(typeof(ConditionKind)))
			{
				if (TacticSchema.UsesThreshold(kind))
					numeric++;
			}
			Assert.Greater(numeric, 0, "수치형이 0 이면 스키마가 통째로 비어버린 것이다(회귀 신호).");
		}
	}
}
