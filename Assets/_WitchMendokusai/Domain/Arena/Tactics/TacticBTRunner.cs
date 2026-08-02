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
					// ★ 공격은 제자리에서 (TASK-WM-194 실측): 이동 명령은 **한 번 주면 계속 유지**되므로,
					//   접근하다가 사거리에 들어 공격 룰로 넘어가도 아무도 이동을 취소하지 않아 유닛이
					//   목표 안으로 계속 걸어 들어갔다 — 개척 마수가 코어에 파묻혀 화면에서 사라진 근본.
					//   "이동 아닌 행동 = 이동 없음" 을 룰 실행 지점에서 명시한다.
					// 발을 멈출지는 *유닛*이 정한다 — 투기장은 서서 싸우고, 개척의 마수는 걸으면서 쏜다.
					if (context.Actuator.StopsToAttack)
						context.Actuator.Hold();
					context.Actuator.UseSkill(rule.Action.SkillSlot, target);
					break;
				case ActionKind.MoveToTarget:
					context.Actuator.MoveToward(target);
					break;
				case ActionKind.Approach:
					// ⚠ TargetQuery.MaxRange 는 **탐색 반경**이지 정지 거리가 아니다 — 그걸 정지 거리로 쓰면
					//   목표가 반경 밖일 때 타겟 자체가 안 잡혀 유닛이 스폰 지점에서 영원히 멈춘다(실측 회귀).
					//   정지 거리는 유닛 쪽 설정(TacticDriver)이 정한다.
					context.Actuator.Approach(target, 0f);
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
