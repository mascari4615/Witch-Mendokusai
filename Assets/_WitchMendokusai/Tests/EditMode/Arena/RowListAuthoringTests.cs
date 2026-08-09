using NUnit.Framework;
// ★ 좌표는 판정 쪽 (TASK-WM-214) — 시험이 구현하는 ICombatant 가 판정 타입을 쓴다.
using Vector3 = WitchMendokusai.Numerics.Vector3;

namespace WitchMendokusai.Tests
{
	/// <summary>
	/// 행 리스트 편집 로직 회귀 — 추가/삭제/순서변경 + 조건/타겟/행동 필드 편집(struct 재대입 정확성).
	/// IR(TacticProgram) 직접 편집. UI 무관(EditMode). WM-165 item 10 코어.
	/// </summary>
	public class RowListAuthoringTests
	{
		[Test]
		public void AddRow_AppendsDefaultRow()
		{
			RowListAuthoring authoring = new(new TacticProgram());
			authoring.AddRow();

			Assert.AreEqual(1, authoring.RowCount);
			TacticRule rule = authoring.Program.Rules[0];
			Assert.AreEqual(ActionKind.MoveToTarget, rule.Action.Kind, "기본 = 접근");
			Assert.AreEqual(1, rule.Conditions.Count);
			Assert.AreEqual(ConditionKind.Always, rule.Conditions[0].Kind);
		}


		// ── 편집기가 오늘 새로 부르기 시작한 setter 3개 ────────────────────────────────
		//
		// ★ 왜 뒤늦게 붙나 (2026-08-06): `RowListAuthoring` 은 setter 8개를 갖췄지만 편집기는 3개만
		//   불렀다(WM-165 item 10). 나머지를 배선하면서 보니 **그 셋은 시험도 없었다** —
		//   아무도 안 부르니 안 깨졌고, 안 깨지니 없어도 티가 안 났다. 이제 편집기가 부르므로 잠근다.
		//   셋 다 struct 재대입 경로(`TargetQuery`/`TacticCondition` 은 struct라 복사본을 고치고 되대입
		//   해야 한다)라, 되대입을 빠뜨리면 **값이 조용히 안 들어간다**. 그게 이 시험이 잡는 것이다.

		[Test]
		public void SetTargetSide_Persists()
		{
			RowListAuthoring authoring = new(new TacticProgram());
			authoring.AddRow();
			Assert.AreEqual(TargetSide.Enemy, authoring.Program.Rules[0].Target.Side, "기본 = 적");

			authoring.SetTargetSide(0, TargetSide.Ally);

			Assert.AreEqual(TargetSide.Ally, authoring.Program.Rules[0].Target.Side,
				"진영이 안 바뀌었다 — struct 되대입 누락이면 여기서 걸린다(아군 대상 전술이 통째로 불가능해진다)");
		}

		[Test]
		public void SetTargetRange_Persists()
		{
			RowListAuthoring authoring = new(new TacticProgram());
			authoring.AddRow();

			authoring.SetTargetRange(0, 7f);

			Assert.AreEqual(7f, authoring.Program.Rules[0].Target.MaxRange, 0.0001f);
		}

		[Test]
		public void SetConditionSkillSlot_Persists()
		{
			RowListAuthoring authoring = new(new TacticProgram());
			authoring.AddRow();
			authoring.SetConditionKind(0, ConditionKind.SkillReady);

			authoring.SetConditionSkillSlot(0, 3);

			Assert.AreEqual(3, authoring.Program.Rules[0].Conditions[0].SkillSlot,
				"조건 슬롯이 안 들어갔다 — 이러면 SkillReady 가 영원히 0번만 본다");
		}

		// 조건이 하나도 없는 룰(출하 프리셋의 fallback 행이 그렇다 — `Conditions: []`)에
		// 슬롯/임계를 넣으면 **조용히 무시**된다. 그게 의도된 계약임을 못박아둔다 —
		// 편집기는 그래서 먼저 SetConditionKind 로 존재를 보장한 뒤 부른다.
		[Test]
		public void 조건이_없으면_슬롯_임계는_조용히_무시된다()
		{
			RowListAuthoring authoring = new(new TacticProgram());
			authoring.AddRow();
			authoring.Program.Rules[0].Conditions.Clear();

			authoring.SetConditionSkillSlot(0, 3);
			authoring.SetConditionThreshold(0, ComparisonOperator.LessThan, 0.5f);

			Assert.AreEqual(0, authoring.Program.Rules[0].Conditions.Count,
				"조건을 새로 만들어내면 안 된다 — 만들 책임은 SetConditionKind 에 있다");
		}

		[Test]
		public void RemoveRow_DropsRow_OutOfRangeNoop()
		{
			RowListAuthoring authoring = new(new TacticProgram());
			authoring.AddRow();
			authoring.AddRow();

			Assert.IsTrue(authoring.RemoveRow(0));
			Assert.AreEqual(1, authoring.RowCount);
			Assert.IsFalse(authoring.RemoveRow(5), "범위 밖 = noop");
			Assert.AreEqual(1, authoring.RowCount);
		}

		[Test]
		public void MoveRow_ReordersPriority()
		{
			RowListAuthoring authoring = new(new TacticProgram());
			authoring.AddRow(); // index 0
			authoring.AddRow(); // index 1
			authoring.SetActionKind(0, ActionKind.UseSkill);
			authoring.SetActionKind(1, ActionKind.Retreat);

			Assert.IsTrue(authoring.MoveRow(1, -1), "index1 위로");
			Assert.AreEqual(ActionKind.Retreat, authoring.Program.Rules[0].Action.Kind, "Retreat 행이 맨 위로");
			Assert.AreEqual(ActionKind.UseSkill, authoring.Program.Rules[1].Action.Kind);

			Assert.IsFalse(authoring.MoveRow(0, -1), "맨 위 = 더 위로 못 감");
		}

		[Test]
		public void SetActionKind_MutatesStructInPlace()
		{
			RowListAuthoring authoring = new(new TacticProgram());
			authoring.AddRow();

			authoring.SetActionKind(0, ActionKind.UseSkill);
			authoring.SetActionSkillSlot(0, 2);

			Assert.AreEqual(ActionKind.UseSkill, authoring.Program.Rules[0].Action.Kind);
			Assert.AreEqual(2, authoring.Program.Rules[0].Action.SkillSlot, "struct 재대입으로 슬롯 반영");
		}

		[Test]
		public void SetTargetPriority_And_ConditionKind_Persist()
		{
			RowListAuthoring authoring = new(new TacticProgram());
			authoring.AddRow();

			authoring.SetTargetPriority(0, TargetPriority.LowestHpRatio);
			authoring.SetConditionKind(0, ConditionKind.TargetHpRatio);
			authoring.SetConditionThreshold(0, ComparisonOperator.LessThan, 0.3f);

			TacticRule rule = authoring.Program.Rules[0];
			Assert.AreEqual(TargetPriority.LowestHpRatio, rule.Target.Priority);
			Assert.AreEqual(ConditionKind.TargetHpRatio, rule.Conditions[0].Kind);
			Assert.AreEqual(ComparisonOperator.LessThan, rule.Conditions[0].Operator);
			Assert.AreEqual(0.3f, rule.Conditions[0].Value, 0.0001f);
		}

		[Test]
		public void EditedProgram_RunsInCompiledRunner()
		{
			// 편집 결과(IR)가 실제 BT 러너로 컴파일·실행되는지 — 편집→거동 연결 회귀.
			RowListAuthoring authoring = new(new TacticProgram());
			authoring.AddRow();
			authoring.SetConditionKind(0, ConditionKind.Always);
			authoring.SetActionKind(0, ActionKind.Retreat);

			FakeCombatant self = new() { CombatantId = 0, TeamId = 0 };
			FakeCombatant enemy = new() { CombatantId = 1, TeamId = 1 };
			FakeResolver resolver = new() { Result = enemy };
			RecordingActuator actuator = new();
			TacticContext context = new(self, resolver, actuator, slot => true);

			TacticBTRunner runner = new(context, authoring.Program);
			runner.UpdateBT();

			Assert.AreEqual("Retreat", actuator.LastAction, "편집한 행동(Retreat)이 실제 실행");
		}

		// --- 스텁 (TacticEngineTests 와 동일 형태) ---
		private sealed class FakeCombatant : ICombatant
		{
			public int CombatantId { get; set; }
			public int TeamId { get; set; }
			public bool IsAlive { get; set; } = true;
			public UnityEngine.Vector3 Position { get; set; }
			public int Hp { get; set; } = 100;
			public int HpMax { get; set; } = 100;
		}

		private sealed class FakeResolver : ITargetResolver
		{
			public ICombatant Result;
			public int AliveCount;
			public ICombatant Query(ICombatant self, TargetQuery query) => Result;
			public int CountAlive(ICombatant self, TargetQuery query) => AliveCount;
		}

		private sealed class RecordingActuator : ITacticActuator
		{
			public string LastAction = "none";
			public void UseSkill(int skillSlot, ICombatant target) => LastAction = "UseSkill";
			public void MoveToward(ICombatant target) => LastAction = "MoveToward";
			public void Approach(ICombatant target, float stopDistance) => LastAction = "Approach";
			public void Retreat(ICombatant target) => LastAction = "Retreat";
			public void Hold() => LastAction = "Hold";
			public bool StopsToAttack => true;
		}
	}
}
