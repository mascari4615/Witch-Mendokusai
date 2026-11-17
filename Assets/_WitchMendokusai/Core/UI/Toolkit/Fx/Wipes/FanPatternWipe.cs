using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>부채 날개들이 동시에 돌며 덮음</summary>
	[CreateAssetMenu(fileName = "Wipe_0_Fan", menuName = "WM/UI/Wipe/Pattern/Fan")]
	public sealed class FanPatternWipe : PatternUIWipe
	{
		[Tooltip("날개 수")]
		public int blades = 8;
		[Tooltip("날개 안에서 바깥부터 차오르는 정도. 0 이면 순수 회전")]
		[Range(0f, 1f)] public float radialMix = 0.15f;

		public override float Order(float x, float y, float aspect)
		{
			float bladeCount = Mathf.Max(1, blades);
			float maxRadius = Mathf.Sqrt(aspect * aspect + 1f) * 0.5f;
			return Mathf.Repeat(WipePatterns.Angle01(x, y, aspect) * bladeCount, 1f) * (1f - radialMix) + (1f - WipePatterns.Radius(x, y, aspect) / maxRadius) * radialMix;
		}
	}
}
