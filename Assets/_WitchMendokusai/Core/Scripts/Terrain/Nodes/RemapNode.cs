using System;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// Remap 노드. height 를 [inMin, inMax] → [outMin, outMax] 로 linear 변환.
	///
	/// 가장 자주 쓰이는 utility — Voronoi (~[-amp, +amp]) 출력을 [0, 100] 같은 의미있는 영역으로 매핑,
	/// FractalPerlin 출력을 ThresholdFilter 의 [0,1] 마스크 영역에 맞추기 등.
	///
	/// `clampToOutRange=true` (기본) 면 입력 범위 밖 → outMin / outMax 로 clamp.
	/// `false` 면 extrapolate (입력 범위 넘어가는 만큼 출력도 넘어감 — 위험할 수 있음).
	///
	/// inMin == inMax 면 0 division 회피 → outMin 반환.
	///
	/// TASK-WM-050 (지형 시스템 후속) sub.
	/// </summary>
	[Serializable]
	public class RemapNode : PointFilterNodeBase
	{
		[Header("Input Range")]
		[SerializeField, Tooltip("입력 범위 하한.")]
		private float inMin = -1f;

		[SerializeField, Tooltip("입력 범위 상한.")]
		private float inMax = 1f;

		[Header("Output Range")]
		[SerializeField, Tooltip("출력 범위 하한.")]
		private float outMin = 0f;

		[SerializeField, Tooltip("출력 범위 상한.")]
		private float outMax = 100f;

		[Header("Clamp")]
		[SerializeField, Tooltip("true 면 입력 범위 밖 → 출력도 outMin/outMax 로 clamp. false 면 extrapolate.")]
		private bool clampToOutRange = true;

		protected override float Evaluate(float height)
		{
			float inRange = inMax - inMin;
			if (Mathf.Abs(inRange) < 1e-6f)
			{
				return outMin;
			}

			float t = (height - inMin) / inRange;
			if (clampToOutRange == true)
			{
				t = Mathf.Clamp01(t);
			}
			return Mathf.Lerp(outMin, outMax, t);
		}
	}
}
