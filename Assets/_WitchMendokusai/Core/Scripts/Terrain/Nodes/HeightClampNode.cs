using System;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// Height clamp 노드. height 를 [minHeight, maxHeight] 범위로 강제.
	///
	/// fundamental utility — 다른 노드 (FractalPerlin, Voronoi 등) 출력 후 안전 범위 제한.
	/// 한쪽만 clamp 하고 싶으면 다른 쪽 기본값 (매우 큰/작은 값) 그대로 두면 효과 X.
	///
	/// Plateau / Beach / Terraced 가 *영역별 평탄화* 라면 본 노드는 *전역 범위 제한*. 두 패턴은 직교.
	///
	/// TASK-WM-050 (지형 시스템 후속) sub.
	/// </summary>
	[Serializable]
	public class HeightClampNode : PointFilterNodeBase
	{
		[Header("Clamp Range")]
		[SerializeField, Tooltip("하한 (m). 입력이 이보다 낮으면 이 값으로 강제. 기본 -1000 = 사실상 비활성.")]
		private float minHeight = -1000f;

		[SerializeField, Tooltip("상한 (m). 입력이 이보다 높으면 이 값으로 강제. 기본 1000 = 사실상 비활성.")]
		private float maxHeight = 1000f;

		protected override float Evaluate(float height)
		{
			if (minHeight > maxHeight)
			{
				return height;
			}
			return Mathf.Clamp(height, minHeight, maxHeight);
		}
	}
}
