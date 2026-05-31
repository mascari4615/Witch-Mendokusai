using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>
	/// 전술 행 리스트 편집 로직 — IR(TacticProgram)을 행 단위로 추가/삭제/순서변경/필드편집.
	/// UI(TacticEditorView)와 분리 = EditMode 검증 가능. v1 = 행당 조건 1개(AND 다중조건은 후순위).
	/// ITacticAuthoring(여러 편집 프론트엔드 → 단일 IR) 의 v1 구현 = 행 리스트가 곧 IR(identity, 별도 compile 0).
	/// 플로차트/FSM·BT 프론트엔드는 후속(같은 TacticProgram 으로 lower).
	/// </summary>
	public class RowListAuthoring
	{
		public TacticProgram Program { get; }

		public RowListAuthoring(TacticProgram program)
		{
			Program = program ?? new TacticProgram();
		}

		public int RowCount => Program.Rules.Count;

		private bool InRange(int index)
		{
			return index >= 0 && index < Program.Rules.Count;
		}

		/// <summary> 기본 행 추가(항상 → 최근접 적 접근) — fallback 행으로 적합. </summary>
		public void AddRow()
		{
			Program.Rules.Add(new TacticRule
			{
				Conditions = new List<TacticCondition> { new TacticCondition { Kind = ConditionKind.Always } },
				Target = new TargetQuery(TargetSide.Enemy, TargetPriority.Nearest, 0f),
				Action = new TacticAction { Kind = ActionKind.MoveToTarget, SkillSlot = 0 },
			});
		}

		public bool RemoveRow(int index)
		{
			if (InRange(index) == false)
				return false;
			Program.Rules.RemoveAt(index);
			return true;
		}

		/// <summary> 행 순서 이동(delta = -1 위 / +1 아래). 위 = 먼저 평가(우선순위 ↑). </summary>
		public bool MoveRow(int index, int delta)
		{
			int target = index + delta;
			if (InRange(index) == false || InRange(target) == false)
				return false;
			TacticRule rule = Program.Rules[index];
			Program.Rules.RemoveAt(index);
			Program.Rules.Insert(target, rule);
			return true;
		}

		// 행 = struct(TacticAction/TargetQuery/TacticCondition) 보유 → 복사본 수정 후 재대입 필요.

		public void SetActionKind(int index, ActionKind kind)
		{
			if (InRange(index) == false)
				return;
			TacticRule rule = Program.Rules[index];
			TacticAction action = rule.Action;
			action.Kind = kind;
			rule.Action = action;
		}

		public void SetActionSkillSlot(int index, int skillSlot)
		{
			if (InRange(index) == false)
				return;
			TacticRule rule = Program.Rules[index];
			TacticAction action = rule.Action;
			action.SkillSlot = skillSlot;
			rule.Action = action;
		}

		public void SetTargetPriority(int index, TargetPriority priority)
		{
			if (InRange(index) == false)
				return;
			TacticRule rule = Program.Rules[index];
			TargetQuery query = rule.Target;
			query.Priority = priority;
			rule.Target = query;
		}

		public void SetTargetSide(int index, TargetSide side)
		{
			if (InRange(index) == false)
				return;
			TacticRule rule = Program.Rules[index];
			TargetQuery query = rule.Target;
			query.Side = side;
			rule.Target = query;
		}

		public void SetTargetRange(int index, float maxRange)
		{
			if (InRange(index) == false)
				return;
			TacticRule rule = Program.Rules[index];
			TargetQuery query = rule.Target;
			query.MaxRange = maxRange;
			rule.Target = query;
		}

		/// <summary> 행 조건 종류(v1 = 단일 조건). 비어 있으면 1개 생성. </summary>
		public void SetConditionKind(int index, ConditionKind kind)
		{
			if (InRange(index) == false)
				return;
			TacticRule rule = Program.Rules[index];
			if (rule.Conditions.Count == 0)
				rule.Conditions.Add(new TacticCondition { Kind = kind });
			else
			{
				TacticCondition condition = rule.Conditions[0];
				condition.Kind = kind;
				rule.Conditions[0] = condition;
			}
		}

		/// <summary> 수치 비교형 조건(SelfHp/HpRatio 등)의 연산자+값. </summary>
		public void SetConditionThreshold(int index, ComparisonOperator op, float value)
		{
			if (InRange(index) == false || Program.Rules[index].Conditions.Count == 0)
				return;
			TacticRule rule = Program.Rules[index];
			TacticCondition condition = rule.Conditions[0];
			condition.Operator = op;
			condition.Value = value;
			rule.Conditions[0] = condition;
		}

		/// <summary> 슬롯 지정 조건(SkillReady)의 스킬 슬롯. </summary>
		public void SetConditionSkillSlot(int index, int skillSlot)
		{
			if (InRange(index) == false || Program.Rules[index].Conditions.Count == 0)
				return;
			TacticRule rule = Program.Rules[index];
			TacticCondition condition = rule.Conditions[0];
			condition.SkillSlot = skillSlot;
			rule.Conditions[0] = condition;
		}
	}
}
