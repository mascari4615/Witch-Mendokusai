using System;
using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.NodeGraph;

namespace WitchMendokusai
{
	/// <summary>out = min(a, b). 낮은 쪽 선택 — 교차 침식, 저지대 우선.</summary>
	[Serializable]
	public class MinNode : NodeBase
	{
		private NodePort<float> inA;
		private NodePort<float> inB;
		private NodePort<float> outHeight;

		protected override IEnumerable<NodePort> CreatePorts()
		{
			inA = new NodePort<float>(this, "a", PortDirection.Input);
			inB = new NodePort<float>(this, "b", PortDirection.Input);
			outHeight = new NodePort<float>(this, "height", PortDirection.Output);
			yield return inA;
			yield return inB;
			yield return outHeight;
		}

		protected override void OnEvaluate(NodeExecutionContext context)
		{
			context.SetOutput(outHeight, Mathf.Min(context.GetInput(inA), context.GetInput(inB)));
		}
	}
}