using System;
using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.NodeGraph;

namespace WitchMendokusai
{
	/// <summary>out = clamp(value, min, max). 고도 범위 강제 제한.</summary>
	[Serializable]
	public class ClampNode : NodeBase
	{
		[SerializeField] private float min = -32f;
		[SerializeField] private float max = 32f;

		private NodePort<float> inHeight;
		private NodePort<float> outHeight;

		protected override IEnumerable<NodePort> CreatePorts()
		{
			inHeight = new NodePort<float>(this, "height", PortDirection.Input);
			outHeight = new NodePort<float>(this, "height", PortDirection.Output);
			yield return inHeight;
			yield return outHeight;
		}

		protected override void OnEvaluate(NodeExecutionContext context)
		{
			context.SetOutput(outHeight, Mathf.Clamp(context.GetInput(inHeight), min, max));
		}
	}
}