using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>별 모양으로 닫힘</summary>
	[CreateAssetMenu(fileName = "Wipe_0_Star", menuName = "WM/UI/Wipe/Pattern/Star")]
	public sealed class StarPatternWipe : PatternUIWipe
	{
		[Tooltip("꼭짓점 수")]
		public int points = 5;
		[Tooltip("꼭짓점 깊이 0~1")]
		[Range(0f, 0.9f)] public float depth = 0.35f;

		public override float Order(float x, float y, float aspect)
		{
			float theta = WipePatterns.Angle01(x, y, aspect) * WipePatterns.TWO_PI;
			float edge = 1f - depth + depth * Mathf.Cos(points * theta);
			return -WipePatterns.Radius(x, y, aspect) / edge;
		}
	}
}
