using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>누른 칸 자리에서 커지며 열리고 그 칸으로 돌아가며 닫힘 (부모와 자식 화면). 누른 칸이 없으면 가운데에서 살짝</summary>
	[CreateAssetMenu(fileName = "Motion_0_ZoomFrom", menuName = "WM/UI/Panel Motion/Zoom From")]
	public sealed class ZoomFromPanelMotion : UIPanelMotion
	{
		public float openSeconds = 0.26f;
		public float closeSeconds = 0.2f;
		[Tooltip("누른 칸이 없을 때 시작 크기")]
		[Range(0f, 1f)] public float fallbackScale = 0.9f;
		[Tooltip("열린 끝에서 넘쳤다 돌아오는 세기")]
		public float overshoot = 0.6f;
		[Tooltip("칸 자리에 있을 때 불투명도")]
		[Range(0f, 1f)] public float startOpacity = 0.4f;

		protected override Action Open(VisualElement panel, VisualElement source, Action onDone)
		{
			// 끊긴 닫기의 변형이 크기 측정에 섞이지 않게
			Restore(panel);
			(Vector2 offset, Vector2 scale) = From(panel, source);
			return UIFxTween.Run(panel, openSeconds, 0f, progress => Apply(panel, offset, scale, 1f - UIFxTween.OutBack(progress, overshoot)), () =>
			{
				Restore(panel);
				onDone();
			});
		}

		protected override Action Close(VisualElement panel, VisualElement source, Action onDone)
		{
			(Vector2 offset, Vector2 scale) = From(panel, source);
			return UIFxTween.Run(panel, closeSeconds, 0f, progress => Apply(panel, offset, scale, UIFxTween.InCubic(progress)), onDone);
		}

		private (Vector2 offset, Vector2 scale) From(VisualElement panel, VisualElement source)
		{
			Rect panelRect = panel.worldBound;
			// 처음 열 때는 아직 레이아웃 전이라 크기가 0 이거나 NaN
			if (source == null || source.panel == null || float.IsNaN(panelRect.width) || panelRect.width <= 0f)
				return (Vector2.zero, new Vector2(fallbackScale, fallbackScale));
			Rect sourceRect = source.worldBound;
			Vector2 offset = sourceRect.center - panelRect.center;
			Vector2 scale = new(sourceRect.width / panelRect.width, sourceRect.height / panelRect.height);
			return (offset, scale);
		}

		// amount 0 은 열린 모습, 1 은 시작 칸 자리
		private void Apply(VisualElement panel, Vector2 offset, Vector2 scale, float amount)
		{
			panel.style.transformOrigin = new TransformOrigin(Length.Percent(50f), Length.Percent(50f));
			panel.style.translate = new Translate(offset.x * amount, offset.y * amount);
			panel.style.scale = new Scale(Vector2.LerpUnclamped(Vector2.one, scale, amount));
			panel.style.opacity = 1f - amount * (1f - startOpacity);
		}
	}
}
