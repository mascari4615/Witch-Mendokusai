using System;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// Beach 노드. 해수면 ~ 해수면+beachExtent 구간을 SmoothStep 으로 평탄화.
	///
	/// 해변 = 해수면 부근 *완만한 모래사장* 표현. seaLevel 위 beachExtent 까지 점진적 평탄화 → 자연스러운 transition.
	/// 해수면 *아래* (수중) 는 건드리지 않음 — Beach 노드는 해변선만 만지고 sea floor 는 다른 노드 (예: 침식) 책임.
	///
	/// 알고리즘:
	///   height < seaLevel               → height 그대로 (수중)
	///   height > seaLevel + beachExtent → height 그대로 (해변 너머 내륙)
	///   그 사이                          → SmoothStep 으로 seaLevel 에서 height 까지 lerp
	///                                     (해변 시작 = 해수면 평면, 해변 끝 = 원 height 복귀)
	///
	/// TASK-WM-050 (지형 시스템 후속) sub.
	/// </summary>
	[Serializable]
	public class BeachFilterNode : PointFilterNodeBase
	{
		[Header("Beach Parameters")]
		[SerializeField, Tooltip("해수면 고도 (m). 이 아래는 그대로 통과 (수중).")]
		private float seaLevel = 0f;

		[SerializeField, Tooltip("해변 폭 (m). seaLevel 부터 위로 이만큼이 평탄화 영향권."), Min(0.001f)]
		private float beachExtent = 6f;

		protected override float Evaluate(float height)
		{
			if (height <= seaLevel)
			{
				return height;
			}

			if (height >= seaLevel + beachExtent)
			{
				return height;
			}

			float beachT = (height - seaLevel) / beachExtent;
			float smoothT = Mathf.SmoothStep(0f, 1f, beachT);
			return Mathf.Lerp(seaLevel, height, smoothT);
		}
	}
}
