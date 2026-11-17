using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>가로 조각이 어긋나며 색이 갈라지는 전환</summary>
	[CreateAssetMenu(fileName = "Wipe_0_Glitch", menuName = "WM/UI/Wipe/Glitch")]
	public sealed class GlitchUIWipe : SingleUIWipe
	{
		public int slices = 18;
		[Tooltip("조각 높이 비율 범위 (가장 얇은 것, 가장 굵은 것)")]
		public Vector2 sliceWeight = new(0.4f, 2f);
		[Tooltip("조각이 어긋나는 최대 거리, 화면 너비 비율")]
		public float shift = 0.12f;
		[Tooltip("색 갈라짐 거리, 픽셀")]
		public float split = 14f;
		[Tooltip("색 갈라짐이 튀는 횟수 (전환 한 번)")]
		public float steps = 14f;
		public Color warm = new(1f, 0.2f, 0.45f, 0.85f);
		public Color cold = new(0.2f, 0.9f, 1f, 0.85f);

		public override IWipeRenderer CreateRenderer() => new GlitchWipe(this);
	}
}
