using System;
using System.Collections.Generic;

namespace WitchMendokusai
{
	/// <summary>
	/// 검증용 dummy 노드 — float 2개 더해 1개 output.
	/// 다중 input pull 검증.
	/// </summary>
	[Serializable]
	public class AddFloatNode : NodeBase
	{
		private NodePort<float> aPort;
		private NodePort<float> bPort;
		private NodePort<float> resultPort;

		protected override IEnumerable<NodePort> CreatePorts()
		{
			aPort = new NodePort<float>(this, "a", PortDirection.Input);
			bPort = new NodePort<float>(this, "b", PortDirection.Input);
			resultPort = new NodePort<float>(this, "result", PortDirection.Output);
			yield return aPort;
			yield return bPort;
			yield return resultPort;
		}

		protected override void OnEvaluate(NodeExecutionContext context)
		{
			float a = context.GetInput(aPort);
			float b = context.GetInput(bPort);
			context.SetOutput(resultPort, a + b);
		}
	}
}
