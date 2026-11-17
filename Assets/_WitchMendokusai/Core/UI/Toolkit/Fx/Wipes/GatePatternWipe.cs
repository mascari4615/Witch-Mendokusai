using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>양옆 문이 가운데로 닫힘</summary>
	[CreateAssetMenu(fileName = "Wipe_0_Gate", menuName = "WM/UI/Wipe/Pattern/Gate")]
	public sealed class GatePatternWipe : PatternUIWipe
	{
		public override float Order(float x, float y, float aspect) => -Mathf.Abs(x - 0.5f);
	}
}
