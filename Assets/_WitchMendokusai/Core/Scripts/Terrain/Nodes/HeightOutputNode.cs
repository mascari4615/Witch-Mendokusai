using System;
using System.Collections.Generic;
using UnityEngine;
using WitchMendokusai.NodeGraph;

namespace WitchMendokusai
{
	/// <summary>
	/// 지형 그래프의 *terminal* — height float 입력 1개. 평가 결과는 context 의 cache 에 남음 (노드 인스턴스 X).
	/// `TerrainGraph.SampleHeight` 가 evaluate 후 `context.GetInput(node.HeightInput)` 으로 cached 값 읽음.
	/// SO 인스턴스 mutation 없음 → background 다발 호출 thread-safe.
	/// </summary>
	[Serializable]
	public class HeightOutputNode : NodeBase
	{
		private NodePort<float> inHeight;

		/// <summary>외부 호출자가 evaluate 후 cached 값 읽기 위한 input port 접근.</summary>
		public NodePort<float> HeightInput
		{
			get
			{
				_ = Ports; // lazy init
				return inHeight;
			}
		}

		protected override IEnumerable<NodePort> CreatePorts()
		{
			inHeight = new NodePort<float>(this, "height", PortDirection.Input);
			yield return inHeight;
		}

		protected override void OnEvaluate(NodeExecutionContext context)
		{
			// 상류 노드 평가 트리거 (side-effect: context output cache 채워짐). 자기 상태는 보유 X.
			_ = context.GetInput(inHeight);
		}
	}
}
