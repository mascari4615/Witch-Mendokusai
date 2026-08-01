using NUnit.Framework;

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
			public ICombatant Query(ICombatant self, TargetQuery query) => Result;
		}

		private sealed class RecordingActuator : ITacticActuator
		{
			public string LastAction = "none";
			public void UseSkill(int skillSlot, ICombatant target) => LastAction = "UseSkill";
			public void MoveToward(ICombatant target) => LastAction = "MoveToward";
			public void Approach(ICombatant target, float stopDistance) => LastAction = "Approach";
			public void Retreat(ICombatant target) => LastAction = "Retreat";
			public void Hold() => LastAction = "Hold";
		}
	}
}
