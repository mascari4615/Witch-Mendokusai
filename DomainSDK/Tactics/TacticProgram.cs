using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>
	/// 한 선수의 전술 = 우선순위 룰 리스트(위→아래 first-match). 순수 데이터 IR(POCO).
	/// 여러 authoring 프론트엔드(행 리스트 / 플로차트 / FSM·BT)가 이 IR 로 컴파일하고,
	/// TacticCompiler(Domain) 가 기존 BT(NodeHelper.Selector) 트리로 lower 한다.
	/// SelectorNode 가 first-match-success 시멘틱이라 위→아래 룰 평가와 정확히 일치.
	/// </summary>
	[Serializable]
	public class TacticProgram
	{
		// 0번부터 평가, 조건 충족되는 첫 룰 실행. 맨 아래 = Always fallback 권장(턴 스킵 방지).
		public List<TacticRule> Rules = new();
	}

	/// <summary> 전술 룰 한 줄 = (조건들 AND) → 타겟 선정 → 행동. </summary>
	[Serializable]
	public class TacticRule
	{
		// 모두 충족(AND)돼야 발동. 비어 있으면 Always 취급.
		public List<TacticCondition> Conditions = new();
		public TargetQuery Target;
		public TacticAction Action;
	}

	/// <summary> 단일 조건. Kind 에 따라 Operator / Value / SkillSlot 사용 여부가 갈린다. </summary>
	[Serializable]
	public struct TacticCondition
	{
		public ConditionKind Kind;
		public ComparisonOperator Operator;
		public float Value;
		// SkillReady 등 슬롯 지정 조건에서만 사용.
		public int SkillSlot;
	}

	/// <summary> 단일 행동. Kind 가 UseSkill 일 때만 SkillSlot 사용. </summary>
	[Serializable]
	public struct TacticAction
	{
		public ActionKind Kind;
		public int SkillSlot;
	}
}
