using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>물결 경계가 위에서 아래로</summary>
	[CreateAssetMenu(fileName = "Wipe_0_Wave", menuName = "WM/UI/Wipe/Pattern/Wave")]
	public sealed class WavePatternWipe : PatternUIWipe
	{
		[Tooltip("물결 수")]
		public float count = 3f;
		[Tooltip("물결 높이, 화면 높이 비율")]
		public float amplitude = 0.07f;

		public override float Order(float x, float y, float aspect) => y + amplitude * Mathf.Sin(x * WipePatterns.TWO_PI * count);
	}
}
