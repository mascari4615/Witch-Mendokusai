using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	// 이름 띠. 평행사변형 면을 그리는 칸, 색과 기울기는 USS 값
	[UxmlElement]
	public partial class DialogueNameBandElement : VisualElement
	{
		private static readonly CustomStyleProperty<Color> BAND_COLOR = new("--wm-dlg-band-color");
		private static readonly CustomStyleProperty<float> BAND_SLANT = new("--wm-dlg-band-slant");

		private Color bandColor = Color.clear;
		private float bandSlant;

		public DialogueNameBandElement()
		{
			pickingMode = PickingMode.Ignore;
			generateVisualContent += Draw;
			RegisterCallback<CustomStyleResolvedEvent>(OnStyleResolved);
		}

		private void OnStyleResolved(CustomStyleResolvedEvent evt)
		{
			if (evt.customStyle.TryGetValue(BAND_COLOR, out Color color))
				bandColor = color;
			if (evt.customStyle.TryGetValue(BAND_SLANT, out float slant))
				bandSlant = slant;
			MarkDirtyRepaint();
		}

		private void Draw(MeshGenerationContext context)
		{
			Rect rect = contentRect;
			Rect outer = new(0f, 0f, resolvedStyle.width, resolvedStyle.height);
			if (rect.width <= 0f || bandColor.a <= 0f)
				return;

			Painter2D painter = context.painter2D;
			painter.fillColor = bandColor;
			painter.BeginPath();
			painter.MoveTo(new Vector2(bandSlant, 0f));
			painter.LineTo(new Vector2(outer.width, 0f));
			painter.LineTo(new Vector2(outer.width - bandSlant, outer.height));
			painter.LineTo(new Vector2(0f, outer.height));
			painter.ClosePath();
			painter.Fill();
		}
	}
}
