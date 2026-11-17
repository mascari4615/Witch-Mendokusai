using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>단색 막 투명도</summary>
	[CreateAssetMenu(fileName = "Wipe_0_Fade", menuName = "WM/UI/Wipe/Fade")]
	public sealed class FadeUIWipe : SingleUIWipe
	{
		public override IWipeRenderer CreateRenderer() => new FadeWipe(this);
	}
}
