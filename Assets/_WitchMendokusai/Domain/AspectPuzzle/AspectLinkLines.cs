using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>붙은 두 칸을 잇는 짧은 선. 칸 위에 겹치는 한 장. 색은 AspectPuzzle.uss, 길이와 굵기는 설정</summary>
	public sealed class AspectLinkLines : VisualElement
	{
		private const string USS_CLASS = "wm-aspect-lines";

		private static readonly CustomStyleProperty<Color> LINE = new("--wm-aspect-link");

		private readonly AspectPuzzleSettings settings;
		private readonly List<(Vector2 From, Vector2 To)> segments = new();
		private Color lineColor;

		public AspectLinkLines(AspectPuzzleSettings settings)
		{
			this.settings = settings;
			pickingMode = PickingMode.Ignore;
			AddToClassList(USS_CLASS);
			generateVisualContent += Draw;
			RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyle);
		}

		/// <summary>칸 가운데끼리의 짝</summary>
		public void Show(IEnumerable<(Vector2 From, Vector2 To)> pairs)
		{
			segments.Clear();
			segments.AddRange(pairs);
			MarkDirtyRepaint();
		}

		private void OnCustomStyle(CustomStyleResolvedEvent evt)
		{
			evt.customStyle.TryGetValue(LINE, out lineColor);
			MarkDirtyRepaint();
		}

		private void Draw(MeshGenerationContext context)
		{
			Painter2D painter = context.painter2D;
			painter.strokeColor = lineColor;
			painter.lineWidth = settings.LinkWidth;
			painter.lineCap = LineCap.Round;
			float start = (1f - settings.LinkLength) * 0.5f;
			foreach ((Vector2 from, Vector2 to) in segments)
			{
				painter.BeginPath();
				painter.MoveTo(Vector2.Lerp(from, to, start));
				painter.LineTo(Vector2.Lerp(from, to, 1f - start));
				painter.Stroke();
			}
		}
	}
}
