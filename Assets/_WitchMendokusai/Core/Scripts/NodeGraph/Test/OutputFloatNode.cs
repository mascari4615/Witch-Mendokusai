using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 검증용 dummy 노드 — float 입력 1개. terminal (output port X) → graph.Execute 의 진입점.
	/// 평가 시 콘솔 로그 + LastValue 캐시.
	/// </summary>
	[Serializable]
	public class OutputFloatNode : NodeBase
	{
		[NonSerialized] private float lastValue;
		public float LastValue => lastValue;

		private NodePort<float> inPort;

		protected override IEnumerable<NodePort> CreatePorts()
		{
			inPort = new NodePort<float>(this, "in", PortDirection.Input);
			yield return inPort;
		}

		protected override void OnEvaluate(NodeExecutionContext context)
		{
			lastValue = context.GetInput(inPort);
			Debug.Log($"[OutputFloatNode] {lastValue}");
		}
	}
}
