using System;
using System.Collections.Generic;
using WitchMendokusai.NodeGraph;
using UnityEngine;

namespace WitchMendokusai
{
	public enum DialogueWaitKind
	{
		Time,
		Event,
	}

	/// <summary>
	/// 대기 노드 (TASK-WM-052 Phase 2 #8). 입력 플로우 `in` + 출력 플로우 `next` — Speak 동형.
	/// traversal 이 이 노드 도달 시 Wait 스텝(<see cref="DialogueWaitKind"/> + <see cref="Seconds"/> +
	/// <see cref="EventId"/>) 방출 → 소비자(DialogueRunner) 가 시간/이벤트 만족 시점에
	/// <see cref="DialogueGraphTraversal.Next"/> 호출 = 대기 완료 신호. traversal 자체는 시간/이벤트 측정 X
	/// (Choice 의 SelectChoice 가 외부 의사결정인 것과 같은 결, 결정성 유지).
	///
	/// Pull executor 사용 X(Speak/Choice 선례) — <see cref="OnEvaluate"/> 무동작. 값은 디자이너 노출
	/// (수치 노출 룰: <see cref="Seconds"/> SerializeField, 런타임 변경 즉시 반영).
	/// </summary>
	[Serializable]
	public class DialogueWaitNode : NodeBase
	{
		public const string PORT_IN = "in";
		public const string PORT_NEXT = "next";

		[SerializeField] private DialogueWaitKind kind = DialogueWaitKind.Time;
		[SerializeField] private float seconds = 1f;
		[SerializeField] private string eventId;

		public DialogueWaitKind Kind { get => kind; set => kind = value; }
		public float Seconds { get => seconds; set => seconds = value; }
		public string EventId { get => eventId; set => eventId = value; }

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
