using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>선택 표시가 다음 칸으로 미끄러지며 크기를 맞추고, 멈추면 맥동. UIFxProfile 의 cursor 칸</summary>
	[CreateAssetMenu(fileName = "Cursor_0_Slide", menuName = "WM/UI/Focus/Cursor")]
	public sealed class UICursorStyle : ScriptableObject
	{
		public Color color = new(1f, 0.45f, 0.72f, 1f);
		[Tooltip("안쪽 채움 세기")]
		[Range(0f, 1f)] public float fillAlpha = 0.22f;
		[Tooltip("테두리 두께, 픽셀")]
		public float borderWidth = 3f;
		public float cornerRadius = 3f;
		[Tooltip("칸보다 바깥으로 나오는 여백, 픽셀")]
		public float padding = 4f;
		[Tooltip("다음 칸으로 가는 시간, 초")]
		public float slideSeconds = 0.12f;
		[Tooltip("도착할 때 넘쳤다 돌아오는 세기")]
		public float overshoot = 1.2f;
		[Tooltip("살짝 기운 각도. 0 이면 반듯")]
		public float tiltDegrees = -1.5f;
		[Tooltip("멈춘 뒤 맥동 한 번 시간과 세기")]
		public float pulseSeconds = 1.1f;
		[Range(0f, 1f)] public float pulseDepth = 0.35f;
		[Tooltip("앞머리 강조 막대 너비, 픽셀. 0 이면 없음")]
		public float accentWidth = 6f;
	}
}
