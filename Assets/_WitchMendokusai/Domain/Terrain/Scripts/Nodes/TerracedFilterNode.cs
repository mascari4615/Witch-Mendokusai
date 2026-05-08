using System;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// Terraced 노드. height → 계단형 평탄화. 농지·계단식 논·인공 단지·고대 유적 같은 *문명*
	/// 시각 표현. tread (평탄 단) + riser (수직/완만 절벽) 비율을 `riserSmoothness` 로 조절.
	///
	/// 알고리즘:
	///   stepBase = Floor(height / stepHeight) * stepHeight
	///   fraction = (height - stepBase) / stepHeight  // 0~1 (한 단 안 위치)
	///   tread 영역 (fraction ≤ 1 - riserSmoothness)  → stepBase 로 강제 (평평)
	///   riser 영역 (그 위)                             → SmoothStep 으로 다음 단까지 lerp
	///
	/// riserSmoothness=0 → 완전 hard 절벽 (계단 자체).
	/// riserSmoothness=1 → 완전 linear (계단 효과 없음, height 그대로).
	///
	/// TASK-WM-050 (지형 시스템 후속) sub.
	/// </summary>
	[Serializable]
	public class TerracedFilterNode : PointFilterNodeBase
	{
		[Header("Terraced Parameters")]
		[SerializeField, Tooltip("계단 한 단의 높이 (m)."), Min(0.001f)]
		private float stepHeight = 4f;

		[SerializeField, Tooltip("0 = hard 절벽 (계단 자체), 1 = 절벽 없음 (height 통과). 중간 = riser 가 SmoothStep 으로 부드럽게."), Range(0f, 1f)]
		private float riserSmoothness = 0f;

		protected override float Evaluate(float height)
		{
			if (stepHeight <= 0f)
			{
				return height;
			}

			float stepIndex = Mathf.Floor(height / stepHeight);
			float stepBase = stepIndex * stepHeight;

			if (riserSmoothness <= 0f)
			{
				return stepBase;
			}

			if (riserSmoothness >= 1f)
			{
				return height;
			}

			float fraction = (height - stepBase) / stepHeight;
			float treadEnd = 1f - riserSmoothness;

			if (fraction <= treadEnd)
			{
				return stepBase;
			}

			float riserT = (fraction - treadEnd) / riserSmoothness;
			float smooth = Mathf.SmoothStep(0f, 1f, riserT);
			return stepBase + stepHeight * smooth;
		}
	}
}
