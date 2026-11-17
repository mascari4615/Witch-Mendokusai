using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>사선 띠가 번갈아 반대 방향에서</summary>
	[CreateAssetMenu(fileName = "Wipe_0_Stripes", menuName = "WM/UI/Wipe/Pattern/Stripes")]
	public sealed class StripesPatternWipe : PatternUIWipe
	{
		[Tooltip("줄 수")]
		public int count = 9;

		public override float Order(float x, float y, float aspect)
		{
			float across = (x * aspect + y) / (aspect + 1f) * Mathf.Max(1, count);
			float along = (x * aspect - y + 1f) / (aspect + 1f);
			bool even = Mathf.FloorToInt(across) % 2 == 0;
			return even ? along : 1f - along;
		}
	}
}
