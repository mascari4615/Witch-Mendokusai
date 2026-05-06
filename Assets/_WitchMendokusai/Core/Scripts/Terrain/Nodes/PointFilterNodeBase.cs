using System;
using System.Collections.Generic;
using WitchMendokusai.NodeGraph;

namespace WitchMendokusai
{
	/// <summary>
	/// 점 단위 height 변환 노드 베이스. Curve / Threshold / Remap 등 stateless point function 공통.
	///
	/// <see cref="RegionGridNodeBase"/> 와 분리 — region 캐시 / N×N sample 의미 X (pure h_in → h_out lookup).
	/// inX/inZ 필요 X — Pull chain 이 GlobalInput 좌표로 source 자동 평가.
	///
	/// H2 (2026-05-06) 신규 — Curve filter 도입과 함께. point function 분류 베이스.
	/// </summary>
	[Serializable]
	public abstract class PointFilterNodeBase : NodeBase
	{
		private NodePort<float> inHeight;
		private NodePort<float> outHeight;

		public NodePort<float> HeightInput
		{
			get
			{
				_ = Ports;
				return inHeight;
			}
		}

		protected override IEnumerable<NodePort> CreatePorts()
		{
			inHeight = new NodePort<float>(this, "height", PortDirection.Input);
			outHeight = new NodePort<float>(this, "height", PortDirection.Output);
			yield return inHeight;
			yield return outHeight;
		}

		protected override void OnEvaluate(NodeExecutionContext context)
		{
			float input = context.GetInput(inHeight);
			float output = Evaluate(input);
			context.SetOutput(outHeight, output);
		}

		/// <summary>sub class 가 점 단위 변환 구현. h_in → h_out.</summary>
		protected abstract float Evaluate(float height);
	}
}
