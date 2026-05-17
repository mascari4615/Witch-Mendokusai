using System;
using System.Collections.Generic;
using WitchMendokusai.NodeGraph;

namespace WitchMendokusai
{
	/// <summary>
	/// 대화 그래프의 진입점. 출력 플로우 포트 `next` 하나만 — <see cref="DialogueGraphTraversal"/> 이
	/// 그래프에서 이 노드를 찾아 traversal 을 시작한다(그래프당 1개 가정, 0개면 빈 대화).
	///
	/// Pull executor 사용 X(QuestNode 선례) — <see cref="OnEvaluate"/> 무동작. 플로우는 traversal 이 구동.
	/// </summary>
	[Serializable]
	public class DialogueStartNode : NodeBase
	{
		public const string PORT_NEXT = "next";

		protected override IEnumerable<NodePort> CreatePorts()
		{
			yield return new NodePort<FlowSignal>(this, PORT_NEXT, PortDirection.Output);
		}

		protected override void OnEvaluate(NodeExecutionContext context)
		{
		}
	}
}
