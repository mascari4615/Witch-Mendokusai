using System;
using System.Collections.Generic;
using WitchMendokusai.NodeGraph;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 조건 분기 노드 (TASK-WM-052 Phase 2 #7). 입력 플로우 `in` + 출력 플로우 `true` / `false`.
	/// traversal 이 이 노드에 닿으면 <see cref="Condition"/> 을 평가해 해당 포트를 *즉시* 따라간다 —
	/// 스텝을 방출하지 않는다(Choice 와의 결정적 차이: 선택지는 *플레이어* 가 고르고, 분기는 *게임 상태* 가 고른다.
	/// 그래서 소비자(DialogueRunner)는 분기 노드의 존재를 알 필요가 없다 = 연출 코드 변경 0).
	///
	/// 조건 표현은 **새로 만들지 않고 기존 <see cref="Criteria"/> 를 그대로 쓴다** —
	/// `RuleEntry.criteria` 선례와 같은 `[SerializeReference]` 다형 직렬화. 아이템 보유
	/// (<see cref="ItemCountCriteria"/>) · 게임 통계(<see cref="GameStatCriteria"/>) · 변수
	/// (<see cref="IntCriteria"/>) 가 그대로 대화 조건이 된다("같은 수치 두 곳 박기" 회피).
	///
	/// Pull executor 사용 X(Speak/Choice/Wait 선례) — <see cref="OnEvaluate"/> 무동작.
	/// </summary>
	[Serializable]
	public class DialogueBranchNode : NodeBase
	{
		public const string PORT_IN = "in";
		public const string PORT_TRUE = "true";
		public const string PORT_FALSE = "false";

		[SerializeReference] private Criteria condition;

		/// <summary>분기 조건. setter = DialogueSpeakNode.Line / ChoiceNode.Options 선례(런타임·테스트 구성).</summary>
		public Criteria Condition { get => condition; set => condition = value; }

		protected override IEnumerable<NodePort> CreatePorts()
		{
			yield return new NodePort<FlowSignal>(this, PORT_IN, PortDirection.Input);
			yield return new NodePort<FlowSignal>(this, PORT_TRUE, PortDirection.Output);
			yield return new NodePort<FlowSignal>(this, PORT_FALSE, PortDirection.Output);
		}

		protected override void OnEvaluate(NodeExecutionContext context)
		{
		}
	}
}
