using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>부드럽게 커지는 대신 정해진 장면 몇 장을 끊어서 바꿔 끼워 열림. 중간 장 몇은 한쪽 축만 펼쳐진 띠</summary>
	[CreateAssetMenu(fileName = "Motion_0_Step", menuName = "WM/UI/Panel Motion/Step")]
	public sealed class StepPanelMotion : UIPanelMotion
	{
		[Min(2)] public int frames = 8;
		[Tooltip("한 장 시간, 초. 0.05 면 초당 20장")]
		public float frameSeconds = 0.05f;
		[Tooltip("닫을 때 한 장 시간")]
		public float closeFrameSeconds = 0.035f;
		[Tooltip("첫 장 크기")]
		[Range(0f, 1f)] public float startScale = 0.25f;
		[Tooltip("장마다 번갈아 기우는 각도 (커질수록 줄어듦)")]
		public float tiltDegrees = 6f;
		[Tooltip("이 번째 장마다 가로로만 펼친 띠. 0 이면 없음")]
		public int bandEvery = 3;
		[Tooltip("띠 장의 세로 비율")]
		[Range(0.05f, 1f)] public float bandThickness = 0.35f;

		protected override Action Open(VisualElement panel, VisualElement source, Action onDone) => Run(panel, false, frameSeconds, onDone);

		protected override Action Close(VisualElement panel, VisualElement source, Action onDone) => Run(panel, true, closeFrameSeconds, onDone);

		private Action Run(VisualElement panel, bool reverse, float seconds, Action onDone)
		{
			int last = -1;
			ApplyFrame(panel, reverse ? frames - 1 : 0);
			return UIFxTween.Run(panel, seconds * frames, 0f, progress =>
			{
				int index = Mathf.Min(frames - 1, Mathf.FloorToInt(progress * frames));
				if (reverse)
					index = frames - 1 - index;
				if (index == last)
					return;
				last = index;
				ApplyFrame(panel, index);
			}, onDone);
		}

		private void ApplyFrame(VisualElement panel, int index)
		{
			float progress = (index + 1) / (float)frames;
			if (index == frames - 1)
			{
				Restore(panel);
				return;
			}
			float scale = Mathf.Lerp(startScale, 1f, progress);
			bool band = bandEvery > 0 && (index + 1) % bandEvery == 0;
			panel.style.scale = band ? new Scale(new Vector2(1f, bandThickness)) : new Scale(new Vector2(scale, scale));
			float tilt = (index % 2 == 0 ? 1f : -1f) * tiltDegrees * (1f - progress);
			panel.style.rotate = UIFxTween.Degrees(band ? 0f : tilt);
			panel.style.opacity = 1f;
		}
	}
}
