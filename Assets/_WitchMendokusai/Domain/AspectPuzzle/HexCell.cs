using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 육각 종이의 칸. 위아래가 뾰족한 육각형을 선으로만 그림 (임시 모습, 사용자 2026-11-25 승인). 색과 굵기는 AspectPuzzle.uss
	/// </summary>
	public sealed class HexCell : AspectCell
	{
		/// <summary>뾰족한 육각형의 너비는 높이의 이만큼</summary>
		public const float WIDTH_PER_HEIGHT = 0.8660254f;
		private const string USS_CLASS = "wm-aspect-hex";

		// 반지름 1 인 육각형의 꼭짓점. 위에서 시계 방향
		private static readonly Vector2[] CORNERS =
		{
			new(0f, -1f),
			new(WIDTH_PER_HEIGHT, -0.5f),
			new(WIDTH_PER_HEIGHT, 0.5f),
			new(0f, 1f),
			new(-WIDTH_PER_HEIGHT, 0.5f),
			new(-WIDTH_PER_HEIGHT, -0.5f),
		};

		private static readonly CustomStyleProperty<Color> FILL = new("--wm-aspect-hex-fill");
		private static readonly CustomStyleProperty<Color> LINE = new("--wm-aspect-hex-line");
		private static readonly CustomStyleProperty<float> LINE_WIDTH = new("--wm-aspect-hex-line-width");

		private Color fill;
		private Color line;
		private float lineWidth;

		public HexCell(Action submitted) : base(submitted)
		{
			AddToClassList(USS_CLASS);
			generateVisualContent += Draw;
			RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyle);
			AddMarks();
		}

		public override bool ContainsPoint(Vector2 localPoint)
		{
			Vector2 half = contentRect.size * 0.5f;
			if (half.x <= 0f || half.y <= 0f)
			{
				return false;
			}
			Vector2 fromCenter = localPoint - contentRect.center;
			float across = Mathf.Abs(fromCenter.x) / half.x;
			float up = Mathf.Abs(fromCenter.y) / half.y;
			// 옆변은 세로. 위아래 빗변은 가운데 꼭짓점에서 옆변까지 높이의 절반만큼 낮아짐
			return across <= 1f && up <= 1f - across * 0.5f;
		}

		private void OnCustomStyle(CustomStyleResolvedEvent evt)
		{
			evt.customStyle.TryGetValue(FILL, out fill);
			evt.customStyle.TryGetValue(LINE, out line);
			evt.customStyle.TryGetValue(LINE_WIDTH, out lineWidth);
			MarkDirtyRepaint();
		}

		private void Draw(MeshGenerationContext context)
		{
			float radius = contentRect.height * 0.5f - lineWidth * 0.5f;
			Painter2D painter = context.painter2D;
			painter.fillColor = fill;
			painter.strokeColor = line;
			painter.lineWidth = lineWidth;
			painter.lineJoin = LineJoin.Miter;
			painter.BeginPath();
			painter.MoveTo(contentRect.center + CORNERS[0] * radius);
			for (int i = 1; i < CORNERS.Length; i++)
			{
				painter.LineTo(contentRect.center + CORNERS[i] * radius);
			}
			painter.ClosePath();
			painter.Fill();
			painter.Stroke();
		}
	}
}
