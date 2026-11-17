using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>브라운관 꺼짐. 위아래가 닫혀 빛줄이 되고 다시 열림</summary>
	[CreateAssetMenu(fileName = "Wipe_0_Tube", menuName = "WM/UI/Wipe/Tube")]
	public sealed class TubeUIWipe : SingleUIWipe
	{
		public Color flashColor = Color.white;
		[Tooltip("닫힌 뒤 남는 빛줄 두께, 픽셀")]
		public float line = 4f;

		public override IWipeRenderer CreateRenderer() => new TubeWipe(this);
	}
}
