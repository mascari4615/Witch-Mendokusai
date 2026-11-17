using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>ScreenWipe 가 맡기는 무늬 하나. UIWipe 에셋의 CreateRenderer 가 만듦</summary>
	public interface IWipeRenderer
	{
		/// <summary>Attach 가 host 에 붙인 최상위 요소. 무늬 교체 때 숨김</summary>
		VisualElement Root { get; }
		void Attach(VisualElement host);
		void Detach();
		void Resize(Vector2 size);
		/// <summary>progress 는 0~1 실제 진행 비율, cover 는 덮는 중인지</summary>
		void Draw(float progress, bool cover);
	}

	/// <summary>
	/// 렌더러 공용 도구. 무늬 종류는 UIWipe 에셋 클래스가 정함
	/// - Fade: 단색 막 투명도
	/// - Tile: 요소 격자가 순서대로 커지며 덮음
	/// - Pattern: 화면 칸마다 덮일 순서 값을 계산해 두고 진행에 따라 경계를 밀며, 경계에 빛
	/// - 층 전환 (Shutter, Slash, Glitch, Tube): 여러 요소가 시차를 두고 움직임. LayeredWipes
	/// </summary>
	public static class WipeRenderers
	{
		internal static VisualElement MakeFill(string elementName)
		{
			VisualElement fill = new() { name = elementName, pickingMode = PickingMode.Ignore };
			fill.style.position = Position.Absolute;
			fill.style.left = 0;
			fill.style.top = 0;
			fill.style.right = 0;
			fill.style.bottom = 0;
			return fill;
		}
	}

	internal sealed class FadeWipe : IWipeRenderer
	{
		private readonly FadeUIWipe preset;
		private VisualElement fill;

		public FadeWipe(FadeUIWipe preset) => this.preset = preset;

		public VisualElement Root => fill;

		public void Attach(VisualElement host)
		{
			fill = WipeRenderers.MakeFill("wipe-fade");
			host.Add(fill);
		}

		public void Detach() => fill?.RemoveFromHierarchy();

		public void Resize(Vector2 size)
		{
		}

		public void Draw(float progress, bool cover)
		{
			float eased = Mathf.SmoothStep(0f, 1f, progress);
			Color color = preset.color;
			color.a *= cover ? eased : 1f - eased;
			fill.style.backgroundColor = color;
		}
	}

	internal sealed class TileWipe : IWipeRenderer
	{
		/// <summary>칸 (열, 행, 열 수, 행 수) 의 덮이는 순서 0~1</summary>
		public delegate float Order(int column, int row, int columns, int rows);

		public static float Diagonal(int column, int row, int columns, int rows) => (column + row) / (float)Mathf.Max(1, columns + rows - 2);

		public static float CenterOut(int column, int row, int columns, int rows)
		{
			Vector2 center = new((columns - 1) * 0.5f, (rows - 1) * 0.5f);
			return Vector2.Distance(new Vector2(column, row), center) / Mathf.Max(1f, center.magnitude);
		}

		public static float Scattered(int column, int row, int columns, int rows) => WipePatterns.Hash01(column, row);

		public static float TopDown(int column, int row, int columns, int rows) => row / (float)Mathf.Max(1, rows - 1);

		// 칸이 너무 많아지지 않게 하는 하한, 조절 대상 아님
		private const float MIN_CELL_PIXELS = 8f;
		// 이웃 칸과 겹쳐 틈을 없애는 최소 배율 (정사각형 칸), 조절 대상 아님
		private const float SQUARE_COVER_GROWTH = 1.02f;
		private const float DIAMOND_ROTATION_DEGREES = 45f;
		private const float CIRCLE_ROUND_PERCENT = 50f;

		private readonly TileUIWipe preset;
		private readonly List<VisualElement> tiles = new();
		private readonly List<float> tileOrder = new();
		private VisualElement container;

		public VisualElement Root => container;

		public TileWipe(TileUIWipe preset) => this.preset = preset;

		private static Order Resolve(TileOrder order) => order switch
		{
			TileOrder.Diagonal => Diagonal,
			TileOrder.CenterOut => CenterOut,
			TileOrder.Scattered => Scattered,
			TileOrder.TopDown => TopDown,
			_ => throw new ArgumentOutOfRangeException(nameof(order), order, "TileWipe 에 등록 안 된 순서"),
		};

		public void Attach(VisualElement host)
		{
			container = WipeRenderers.MakeFill("wipe-tiles");
			host.Add(container);
		}

		public void Detach() => container?.RemoveFromHierarchy();

		public void Resize(Vector2 size)
		{
			container.Clear();
			tiles.Clear();
			tileOrder.Clear();
			// 순서는 만들 때마다 에셋에서 읽음 (실행 중 인스펙터 변경 반영)
			Order order = Resolve(preset.order);
			float cell = Mathf.Max(MIN_CELL_PIXELS, preset.cellSize);
			bool row = preset.shape == TileShape.Row;
			int columns = row ? 1 : Mathf.CeilToInt(size.x / cell) + 1;
			int rows = Mathf.CeilToInt(size.y / cell) + 1;
			for (int rowIndex = 0; rowIndex < rows; rowIndex++)
			{
				for (int column = 0; column < columns; column++)
				{
					VisualElement tile = new() { pickingMode = PickingMode.Ignore };
					tile.style.position = Position.Absolute;
					tile.style.width = row ? size.x + cell : cell;
					tile.style.height = cell;
					tile.style.left = row ? -cell * 0.5f : column * cell - cell * 0.5f;
					tile.style.top = row ? rowIndex * cell : rowIndex * cell - cell * 0.5f;
					if (preset.shape == TileShape.Diamond)
						tile.style.rotate = new UnityEngine.UIElements.Rotate(new UnityEngine.UIElements.Angle(DIAMOND_ROTATION_DEGREES, AngleUnit.Degree));
					if (preset.shape == TileShape.Circle)
					{
						Length radius = Length.Percent(CIRCLE_ROUND_PERCENT);
						tile.style.borderTopLeftRadius = radius;
						tile.style.borderTopRightRadius = radius;
						tile.style.borderBottomLeftRadius = radius;
						tile.style.borderBottomRightRadius = radius;
					}
					tile.style.scale = new Scale(Vector3.zero);
					container.Add(tile);
					tiles.Add(tile);
					tileOrder.Add(order(column, rowIndex, columns, rows));
				}
			}
		}

		public void Draw(float progress, bool cover)
		{
			float spread = preset.spread;
			float growth = preset.shape == TileShape.Square || preset.shape == TileShape.Row ? SQUARE_COVER_GROWTH : preset.growth;
			for (int i = 0; i < tiles.Count; i++)
			{
				float local = Mathf.Clamp01(progress * (1f + spread) - tileOrder[i] * spread);
				float grown;
				// 픽셀 디졸브: 칸이 한 번에 켜지고 꺼짐. 줄어드는 칸 틈이 격자선처럼 보여서
				if (preset.shape == TileShape.Square)
					grown = (local >= 0.5f) == cover ? 1f : 0f;
				else
					grown = cover ? (preset.overshoot ? EaseOutBack(local) : EaseOutCubic(local)) : 1f - local * local;
				float scale = Mathf.Max(0f, grown) * growth;
				tiles[i].style.scale = preset.shape == TileShape.Row ? new Scale(new Vector3(1f, scale, 1f)) : new Scale(new Vector3(scale, scale, 1f));
				tiles[i].style.backgroundColor = preset.color;
			}
		}

		private static float EaseOutBack(float progress)
		{
			const float OVERSHOOT = 1.4f;
			float shifted = progress - 1f;
			return 1f + (OVERSHOOT + 1f) * shifted * shifted * shifted + OVERSHOOT * shifted * shifted;
		}

		private static float EaseOutCubic(float progress) => 1f - Mathf.Pow(1f - progress, 3f);
	}
}
