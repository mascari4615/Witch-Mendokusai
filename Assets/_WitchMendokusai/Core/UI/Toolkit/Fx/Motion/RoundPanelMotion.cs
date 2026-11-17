using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>모서리가 둥글어지며 원으로 오므라들어 닫힘. 열기는 반대로 원에서 펴짐</summary>
	[CreateAssetMenu(fileName = "Motion_0_Round", menuName = "WM/UI/Panel Motion/Round")]
	public sealed class RoundPanelMotion : UIPanelMotion
	{
		public float openSeconds = 0.28f;
		public float closeSeconds = 0.26f;
		[Tooltip("오므라든 끝 크기")]
		[Range(0f, 1f)] public float endScale = 0.05f;
		[Tooltip("끝에서 사라지는 구간 비율")]
		[Range(0.05f, 1f)] public float fadePart = 0.3f;

		protected override Action Open(VisualElement panel, VisualElement source, Action onDone) =>
			UIFxTween.Run(panel, openSeconds, 0f, progress => Apply(panel, 1f - UIFxTween.OutCubic(progress)), () =>
			{
				Restore(panel);
				onDone();
			});

		protected override Action Close(VisualElement panel, VisualElement source, Action onDone) =>
			UIFxTween.Run(panel, closeSeconds, 0f, progress => Apply(panel, UIFxTween.InCubic(progress)), onDone);

		// amount 0 은 원래 모습, 1 은 완전히 오므라든 원
		private void Apply(VisualElement panel, float amount)
		{
			panel.style.overflow = Overflow.Hidden;
			// 앞 절반에 원이 되고, 마지막 fadePart 구간에 사라짐
			UIFxTween.SetRadius(panel, Length.Percent(50f * Mathf.Clamp01(amount * 2f)));
			float scale = Mathf.Lerp(1f, endScale, amount);
			panel.style.scale = new Scale(new Vector2(scale, scale));
			panel.style.opacity = 1f - Mathf.Clamp01((amount - (1f - fadePart)) / fadePart);
		}
	}
}
