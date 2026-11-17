using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>12시부터 시계 방향으로 쓸기</summary>
	[CreateAssetMenu(fileName = "Wipe_0_Clock", menuName = "WM/UI/Wipe/Pattern/Clock")]
	public sealed class ClockPatternWipe : PatternUIWipe
	{
		public override float Order(float x, float y, float aspect) => WipePatterns.Angle01(x, y, aspect);
	}
}
