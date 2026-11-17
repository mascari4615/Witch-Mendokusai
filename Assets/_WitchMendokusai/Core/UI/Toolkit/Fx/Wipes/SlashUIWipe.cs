using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>사선이 그어지고 화면이 차오른 뒤, 그 선을 따라 둘로 갈라지며 걷힘</summary>
	[CreateAssetMenu(fileName = "Wipe_0_Slash", menuName = "WM/UI/Wipe/Slash")]
	public sealed class SlashUIWipe : SingleUIWipe
	{
		public Color accentColor = new(1f, 0.55f, 0.75f, 1f);
		[Tooltip("사선 두께, 픽셀")]
		public float accentLine = 6f;
		public Color flashColor = Color.white;
		[Range(0f, 1f)] public float flashAlpha = 0.85f;
		public float angle = 24f;

		public override IWipeRenderer CreateRenderer() => new SlashWipe(this);
	}
}
