using System;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// Plateau / 메사 노드. height → targetHeight 주변 평탄화.
	///
	/// 입력 height 가 [targetHeight - plateauWidth/2, targetHeight + plateauWidth/2] 안이면 targetHeight 로 강제 (메사 정상부).
	/// 그 범위 외 + blendWidth 안이면 smooth step 으로 부드럽게 원 height 로 복귀.
	/// blendWidth 밖이면 입력 그대로 통과.
	///
	/// Threshold + Curve 의 사이 — 특정 고도대 *집중적 평탄화*. 메사 / 고원 / 인공 단지.
	/// TASK-WM-050 (지형 시스템 후속) sub.
	/// </summary>
	[Serializable]
	public class PlateauFilterNode : PointFilterNodeBase
	{
		[Header("Plateau Parameters")]
		[SerializeField, Tooltip("평탄화 기준 고도 (m).")]
		private float targetHeight = 30f;

		[SerializeField, Tooltip("평탄화 영역 폭 (m). 입력 height 가 target ± width/2 안이면 target 으로 강제."), Min(0f)]
		private float plateauWidth = 8f;

		[SerializeField, Tooltip("경계 smooth blend 폭 (m). 0 이면 hard 절벽."), Min(0f)]
		private float blendWidth = 6f;

		protected override float Evaluate(float height)
		{
			float distance = Mathf.Abs(height - targetHeight);
			float halfPlateau = plateauWidth * 0.5f;

			if (distance <= halfPlateau)
			{
				return targetHeight;
			}

			if (blendWidth <= 0f || distance >= halfPlateau + blendWidth)
			{
				return height;
			}

			// 경계 — distance 가 halfPlateau ~ halfPlateau+blendWidth 구간.
			// t=0 (안쪽 경계) → targetHeight, t=1 (바깥 경계) → 원 height.
			float blendT = Mathf.SmoothStep(0f, 1f, (distance - halfPlateau) / blendWidth);
			return Mathf.Lerp(targetHeight, height, blendT);
		}
	}
}
