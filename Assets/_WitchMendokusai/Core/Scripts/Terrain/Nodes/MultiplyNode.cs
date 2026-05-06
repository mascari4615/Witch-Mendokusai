using System;
using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.NodeGraph;

namespace WitchMendokusai
{
	/// <summary>out = a * b. 마스킹, 진폭 스케일.</summary>
	[Serializable]
	public class MultiplyNode : NodeBase
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
			context.SetOutput(outHeight, context.GetInput(inA) * context.GetInput(inB));
		}
	}
}