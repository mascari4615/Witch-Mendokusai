using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>바깥에서 안으로 감기는 나선</summary>
	[CreateAssetMenu(fileName = "Wipe_0_Spiral", menuName = "WM/UI/Wipe/Pattern/Spiral")]
	public sealed class SpiralPatternWipe : PatternUIWipe
	{
		[Tooltip("감김 수")]
		public float turns = 2f;

		public override float Order(float x, float y, float aspect)
		{
			float maxRadius = Mathf.Sqrt(aspect * aspect + 1f) * 0.5f;
			return WipePatterns.Angle01(x, y, aspect) + turns * (1f - WipePatterns.Radius(x, y, aspect) / maxRadius);
		}
	}
}
