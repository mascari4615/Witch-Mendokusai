using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>비스듬한 띠들이 차례로 밀려 들어와 덮고 같은 방향으로 나가며 걷힘</summary>
	[CreateAssetMenu(fileName = "Wipe_0_Shutter", menuName = "WM/UI/Wipe/Shutter")]
	public sealed class ShutterUIWipe : SingleUIWipe
	{
		[Tooltip("띠 앞머리 강조색")]
		public Color accentColor = new(1f, 0.55f, 0.75f, 1f);
		[Tooltip("강조선 두께, 픽셀")]
		public float accentLine = 6f;
		public int bands = 6;
		public float angle = 16f;
		[Tooltip("이웃 띠와 겹치는 너비 배율 (경계 틈 방지)")]
		public float overlap = 1.2f;
		[Tooltip("띠의 출발 거리와 퇴장 거리, 화면 너비 배수")]
		public float travel = 1.6f;
		[Tooltip("첫 띠와 끝 띠의 시작 간격, 전체 시간 비율")]
		[Range(0f, 0.9f)] public float stagger = 0.45f;
		[Tooltip("홀수 띠가 강조색 쪽으로 물드는 정도")]
		[Range(0f, 1f)] public float tint = 0.18f;

		public override IWipeRenderer CreateRenderer() => new ShutterWipe(this);
	}
}
