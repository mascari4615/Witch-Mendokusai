using System;
using System.Collections.Generic;
using WitchMendokusai.NodeGraph;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 한 라인을 말하는 노드 — Phase 1 의 <see cref="DialogueLine"/> SO 를 그대로 참조(평행 데이터 X).
	/// 입력 플로우 `in` · 출력 플로우 `next`. traversal 이 이 노드 도달 시 Speak 스텝을 방출하고
	/// `next` 연결을 따라 다음 노드로(연결 없으면 대화 종료).
	///
	/// Pull executor 사용 X — <see cref="OnEvaluate"/> 무동작. 출력 연출(typewriter/portrait/sfx)은
	/// 소비자(DialogueRunner)가 <see cref="Line"/> 를 읽어 수행(DialogueRunner 의 책임 분리 일관).
	/// </summary>
	[Serializable]
	public class DialogueSpeakNode : NodeBase
	{
		public const string PORT_IN = "in";
		public const string PORT_NEXT = "next";

		[SerializeField] private DialogueLine line;

		public DialogueLine Line { get => line; set => line = value; }

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
