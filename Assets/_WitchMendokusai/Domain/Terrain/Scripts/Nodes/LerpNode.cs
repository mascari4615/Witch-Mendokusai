using System;
using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.NodeGraph;

namespace WitchMendokusai
{
	/// <summary>
	/// Lerp 노드. Lerp(a, b, t) — 두 height 소스 간 blend.
	///
	/// t = 0 → a, t = 1 → b. ThresholdFilterNode 결과를 t 에 연결해 고도별 noise 전환.
	/// inA/inB/inT 미연결 시 0.0 fallback.
	///
	/// H3 (2026-05-06) 신규.
	/// </summary>
	[Serializable]
	[NodeDomain(NodeDomain.Terrain)]
	public class LerpNode : NodeBase
	{
		private NodePort<float> inA;
		private NodePort<float> inB;
		private NodePort<float> inT;
		private NodePort<float> outHeight;

		protected override IEnumerable<NodePort> CreatePorts()
		{
			inA = new NodePort<float>(this, "a", PortDirection.Input);
			inB = new NodePort<float>(this, "b", PortDirection.Input);
			inT = new NodePort<float>(this, "t", PortDirection.Input);
			outHeight = new NodePort<float>(this, "height", PortDirection.Output);
			yield return inA;
			yield return inB;
			yield return inT;
			yield return outHeight;
		}

		protected override void OnEvaluate(NodeExecutionContext context)
		{
			float a = context.GetInput(inA);
			float b = context.GetInput(inB);
			float t = context.GetInput(inT);
			context.SetOutput(outHeight, Mathf.Lerp(a, b, t));
		}
	}
}
