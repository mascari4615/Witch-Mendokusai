using System;
using UnityEngine;
using WitchMendokusai.NodeGraph;

namespace WitchMendokusai
{
	/// <summary>
	/// Threshold filter 노드. height → smooth step mask [0,1].
	///
	/// `threshold` 기준으로 아래 = 0, 위 = 1. `blendWidth` 로 경계 부드럽게.
	/// `blendWidth=0` 이면 hard step (0 or 1).
	/// LerpNode 의 t 입력으로 연결해 두 height 간 고도별 blend.
	///
	/// H3 (2026-05-06) 신규.
	/// </summary>
	[Serializable]
	[NodeDomain(NodeDomain.Terrain)]
	public class ThresholdFilterNode : PointFilterNodeBase
	{
		[Header("Threshold Parameters")]
		[SerializeField, Tooltip("전환 기준 고도 (m).")]
		private float threshold = 25f;

		[SerializeField, Tooltip("전환 구간 폭 (m). 0 이면 hard step."), Min(0f)]
		private float blendWidth = 5f;

		protected override float Evaluate(float height)
		{
			if (blendWidth <= 0f)
				return height >= threshold ? 1f : 0f;

			float lower = threshold - blendWidth * 0.5f;
			float upper = threshold + blendWidth * 0.5f;
			return Mathf.SmoothStep(lower, upper, height);
		}
	}
}
