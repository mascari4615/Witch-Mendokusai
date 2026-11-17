using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>구름 무늬로 녹아 덮임</summary>
	[CreateAssetMenu(fileName = "Wipe_0_Dissolve", menuName = "WM/UI/Wipe/Pattern/Dissolve")]
	public sealed class DissolvePatternWipe : PatternUIWipe
	{
		[Tooltip("구름 크기. 클수록 잘게")]
		public float scale = 5f;

		public override float Order(float x, float y, float aspect) => WipePatterns.Fractal(x * aspect * scale, y * scale);
	}
}
