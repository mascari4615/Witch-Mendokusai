using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>원이 가장자리에서 가운데로 닫힘</summary>
	[CreateAssetMenu(fileName = "Wipe_0_Iris", menuName = "WM/UI/Wipe/Pattern/Iris")]
	public sealed class IrisPatternWipe : PatternUIWipe
	{
		public override float Order(float x, float y, float aspect) => -WipePatterns.Radius(x, y, aspect);
	}
}
