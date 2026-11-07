using System;
using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.NodeGraph;

namespace WitchMendokusai
{
	// 무대 신호 노드. 보이는 스텝 없이 지나감, 대사 흐름 순서 유지
	[Serializable]
	public class DialogueStageNode : NodeBase
	{
		public const string PORT_IN = "in";
		public const string PORT_NEXT = "next";

		[SerializeField] private List<DialogueStageCue> cues = new();

		public List<DialogueStageCue> Cues { get => cues; set => cues = value ?? new(); }

		protected override IEnumerable<NodePort> CreatePorts()
		{
			yield return new NodePort<FlowSignal>(this, PORT_IN, PortDirection.Input);
			yield return new NodePort<FlowSignal>(this, PORT_NEXT, PortDirection.Output);
		}

		protected override void OnEvaluate(NodeExecutionContext context)
		{
		}
	}
}
