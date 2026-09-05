using System;

namespace WitchMendokusai
{
	/// <summary>
	/// Height offset 노드. 단순 height + offset.
	///
	/// 가장 fundamental utility — 모든 노드 chain 의 마지막 단에 두어 "전체 지형 N 미터 들어올리기 / 내리기".
	/// `RemapNode` 의 outMin 만 조정하는 것과 의미상 비슷하지만 input 범위 가정 없는 *순수 평행이동*.
	///
	/// TASK-WM-050 (지형 시스템 후속) sub.
	/// </summary>
	[Serializable]
	public class HeightOffsetNode : PointFilterNodeBase
	{
		[UnityEngine.SerializeField, UnityEngine.Tooltip("입력 height 에 더할 값 (m). 양수 = 위로, 음수 = 아래로.")]
		private float offset = 0f;

		protected override float Evaluate(float height)
		{
			return height + offset;
		}
	}
}
