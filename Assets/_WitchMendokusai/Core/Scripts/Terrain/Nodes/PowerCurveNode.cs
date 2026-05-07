using System;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// Power curve 노드. height 에 power exponent 적용 → 산 *가팔라짐* / *완만함* 곡선 조절.
	///
	/// `exponent > 1`: 낮은 부분 더 낮아짐 (낮은 지대가 *깊어짐*) — 산 정상부가 상대적으로 가팔라 보임.
	/// `exponent < 1`: 낮은 부분 더 높아짐 (낮은 지대가 *얕아짐*) — 전반적 완만.
	/// `exponent = 1`: 통과.
	///
	/// `preserveSign=true` (기본): height 음수 영역도 동일 곡선 적용 (sign × pow(|height|, exp)).
	/// `false`: 음수 영역 → 0 으로 clamp.
	///
	/// Curve filter (AnimationCurve 임의 곡선) 의 단순화 버전 — 1 파라미터로 산 모양 조절.
	///
	/// TASK-WM-050 (지형 시스템 후속) sub.
	/// </summary>
	[Serializable]
	public class PowerCurveNode : PointFilterNodeBase
	{
		[Header("Power Curve")]
		[SerializeField, Tooltip(">1 = 가파른 산 (낮은 지대 더 깊게), <1 = 완만 (낮은 지대 더 얕게), =1 = 통과."), Range(0.1f, 5f)]
		private float exponent = 1.5f;

		[SerializeField, Tooltip("true: 음수 height 도 동일 곡선 (sign 보존). false: 음수 → 0 clamp.")]
		private bool preserveSign = true;

		protected override float Evaluate(float height)
		{
			if (Mathf.Approximately(exponent, 1f) == true)
			{
				return height;
			}

			if (preserveSign == true)
			{
				float sign = height < 0f ? -1f : 1f;
				return sign * Mathf.Pow(Mathf.Abs(height), exponent);
			}

			return Mathf.Pow(Mathf.Max(0f, height), exponent);
		}
	}
}
