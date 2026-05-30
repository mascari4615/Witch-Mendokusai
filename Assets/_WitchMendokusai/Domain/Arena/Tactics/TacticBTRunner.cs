using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>
	/// TacticProgram(IR) → 기존 BT 트리(NodeHelper.Selector(rows))로 lazy 컴파일해 평가하는 BTRunner.
	/// 각 룰 = Sequence(Condition(조건 AND + 타겟 해석), Action(행동 실행)). SelectorNode first-match 가
	/// 위→아래 우선순위(UO/FF12 시멘틱)와 정확히 일치. 행동은 ITacticActuator(테스트 fake 가능).
	/// ★ BTRunner ctor 가 unitObject 세팅 전 MakeNode 호출 → program 가용한 첫 평가 시 lazy 빌드.
	/// </summary>
	public class TacticBTRunner : BTRunner
	{
		private readonly TacticContext context;
		private TacticProgram program;
		private Node compiledRoot;
		// EvalRow → ExecRow 사이 같은-틱 타겟 전달(Sequence 가 둘을 연속 실행).
		private ICombatant resolvedTarget;

		public TacticBTRunner(TacticContext context, TacticProgram program) : base(null)
		{
			this.context = context;
			this.program = program;
		}

		protected override Node MakeNode()
		{
			// base ctor 시점엔 program/context 가 아직 null → lazy. RunCompiled 가 가용 시 빌드.
			return NodeHelper.Action(RunCompiled);
		}

		/// <summary> 일시정지 중 룰 재편집 핫스왑 — 다음 평가에 재컴파일. </summary>
		public void SetProgram(TacticProgram newProgram)
		{
			program = newProgram;
			compiledRoot = null;
		}

		private BTState RunCompiled()
		{
			compiledRoot ??= Compile();
			return compiledRoot.UpdateBT();
		}

		private Node Compile()
		{
			List<Node> rows = new();
			if (program != null)
			{
				foreach (TacticRule rule in program.Rules)
					rows.Add(BuildRow(rule));
			}
			return NodeHelper.Selector(rows.ToArray());
		}

		private Node BuildRow(TacticRule rule)
		{
			return NodeHelper.Sequence(
				NodeHelper.Condition(() => EvalRow(rule)),
				NodeHelper.Action(() => ExecRow(rule)));
		}

		private bool EvalRow(TacticRule rule)
		{
			bool ok = TacticConditions.EvalRule(rule, context, out ICombatant target);
			if (ok)
				resolvedTarget = target;
			return ok;
		}

		private BTState ExecRow(TacticRule rule)
		{
			ICombatant target = resolvedTarget;
			switch (rule.Action.Kind)
			{
				case ActionKind.UseSkill:
					context.Actuator.UseSkill(rule.Action.SkillSlot, target);
					break;
				case ActionKind.MoveToTarget:
				case ActionKind.Approach:
					context.Actuator.MoveToward(target);
					break;
				case ActionKind.Retreat:
					context.Actuator.Retreat(target);
					break;
				default:
					context.Actuator.Hold();
					break;
			}

			// 즉시 Success → Selector 가 매 틱 top 부터 재평가(우선순위 인터럽트 가능).
			return BTState.Success;
		}
	}
}
