using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>가운데에서 물결치며 퍼짐</summary>
	[CreateAssetMenu(fileName = "Wipe_0_Ripple", menuName = "WM/UI/Wipe/Pattern/Ripple")]
	public sealed class RipplePatternWipe : PatternUIWipe
	{
		[Tooltip("물결 높이와 촘촘함")]
		public float amplitude = 0.035f;
		public float frequency = 48f;

		public override float Order(float x, float y, float aspect)
		{
			float radius = WipePatterns.Radius(x, y, aspect);
			// 물결 폭이 커서 바깥 고리가 먼저 덮이는 구간이 생김. 고리 무늬로 의도
			return radius + amplitude * Mathf.Sin(radius * frequency);
		}
	}
}
