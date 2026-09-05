using System;
using UnityEngine;
using WitchMendokusai.NodeGraph;

namespace WitchMendokusai
{
	/// <summary>
	/// Curve filter 노드. AnimationCurve 로 height 직접 remap — 디자이너 GUI 직접 조작.
	///
	/// 도메인: [minHeight, maxHeight] → [0,1] 정규화 → curve.Evaluate(t) → [minHeight, maxHeight] denormalize.
	/// 도메인 밖 입력은 passthrough (회귀 X 보장 + 디자이너 도메인 명시).
	///
	/// 디폴트 Linear [0,0]→[1,1] = no-op. 디자이너 Curve 조정 후 차이 시각.
	///
	/// H2 (2026-05-06) 신규.
	/// </summary>
	[Serializable]
	[NodeDomain(NodeDomain.Terrain)]
	public class CurveFilterNode : PointFilterNodeBase
	{
		[Header("Curve Parameters")]
		[SerializeField, Tooltip("입력 height 의 [minHeight, maxHeight] 범위 안에서 remap. 도메인 밖은 passthrough.")]
		private AnimationCurve curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

		[SerializeField, Tooltip("정규화 도메인 최솟값 (m).")]
		private float minHeight = 0f;

		[SerializeField, Tooltip("정규화 도메인 최댓값 (m).")]
		private float maxHeight = 50f;

		protected override float Evaluate(float height)
		{
			float range = maxHeight - minHeight;
			if (range <= 0f)
				return height;
			if (height < minHeight || height > maxHeight)
				return height;
			float normalizedT = (height - minHeight) / range;
			return minHeight + curve.Evaluate(normalizedT) * range;
		}
	}
}
