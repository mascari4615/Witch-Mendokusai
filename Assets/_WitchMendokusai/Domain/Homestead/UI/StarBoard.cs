using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using WitchMendokusai.DomainSDK.Homestead;

namespace WitchMendokusai
{
	// 별 잇기 판. 별은 누르는 동그라미, 선은 그림 (안내 선은 흐리게, 그은 선은 밝게)
	// 별 자리는 0~1 판 좌표 (왼쪽 아래 0) 라 위아래를 뒤집어 놓음. 색은 USS 사용자 속성
	public sealed class StarBoard : VisualElement
	{
		private const string BLOCK = "wm-hs-star";

		private static readonly CustomStyleProperty<Color> GUIDE_LINE = new("--wm-hs-star-guide");
		private static readonly CustomStyleProperty<Color> DRAWN_LINE = new("--wm-hs-star-line");
		private static readonly CustomStyleProperty<float> GUIDE_WIDTH = new("--wm-hs-star-guide-width");
		private static readonly CustomStyleProperty<float> DRAWN_WIDTH = new("--wm-hs-star-line-width");

		private readonly List<VisualElement> starViews = new();
		private StarLink link;
		private Color guideColor = new(1f, 1f, 1f, 0.15f);
		private Color drawnColor = Color.white;
		private float guideWidth = 2f;
		private float drawnWidth = 4f;

		public event Action<int> Pressed = delegate { };

		public StarBoard()
		{
			AddToClassList(BLOCK + "__board");
			generateVisualContent += DrawLines;
			RegisterCallback<CustomStyleResolvedEvent>(OnStyle);
		}

		public void Show(StarLink shown)
		{
			link = shown;
			foreach (VisualElement view in starViews)
				view.RemoveFromHierarchy();
			starViews.Clear();
			for (int i = 0; i < link.Constellation.Stars.Count; i++)
			{
				float[] star = link.Constellation.Stars[i];
				int index = i;
				VisualElement view = new() { pickingMode = PickingMode.Position };
				view.AddToClassList(BLOCK + "__dot");
				view.style.left = Length.Percent(star[0] * 100f);
				view.style.top = Length.Percent((1f - star[1]) * 100f);
				view.RegisterCallback<PointerDownEvent>(evt =>
				{
					Pressed(index);
					evt.StopPropagation();
				});
				Add(view);
				starViews.Add(view);
			}
			Refresh();
		}

		public void Refresh()
		{
			if (link == null)
				return;
			for (int i = 0; i < starViews.Count; i++)
				starViews[i].EnableInClassList(BLOCK + "__dot--selected", i == link.Selected);
			EnableInClassList(BLOCK + "__board--done", link.IsDone);
			MarkDirtyRepaint();
		}

		private void OnStyle(CustomStyleResolvedEvent evt)
		{
			ICustomStyle custom = evt.customStyle;
			if (custom.TryGetValue(GUIDE_LINE, out Color guide))
				guideColor = guide;
			if (custom.TryGetValue(DRAWN_LINE, out Color drawn))
				drawnColor = drawn;
			if (custom.TryGetValue(GUIDE_WIDTH, out float guideSize))
				guideWidth = guideSize;
			if (custom.TryGetValue(DRAWN_WIDTH, out float drawnSize))
				drawnWidth = drawnSize;
			MarkDirtyRepaint();
		}

		private Vector2 PointOf(int star)
		{
			float[] position = link.Constellation.Stars[star];
			Rect rect = contentRect;
			return new Vector2(rect.xMin + position[0] * rect.width, rect.yMin + (1f - position[1]) * rect.height);
		}

		private void DrawLines(MeshGenerationContext context)
		{
			if (link == null)
				return;
			Painter2D painter = context.painter2D;
			painter.lineCap = LineCap.Round;
			for (int i = 0; i < link.Constellation.Lines.Count; i++)
			{
				int[] line = link.Constellation.Lines[i];
				bool drawn = link.IsDrawn(i);
				painter.strokeColor = drawn ? drawnColor : guideColor;
				painter.lineWidth = drawn ? drawnWidth : guideWidth;
				painter.BeginPath();
				painter.MoveTo(PointOf(line[0]));
				painter.LineTo(PointOf(line[1]));
				painter.Stroke();
			}
		}
	}
}
