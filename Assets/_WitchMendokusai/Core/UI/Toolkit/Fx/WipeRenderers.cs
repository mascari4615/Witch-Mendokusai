using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>ScreenWipe 가 맡기는 무늬 하나. 새 무늬는 이것을 구현하고 WipeRenderers.Create 에 한 줄</summary>
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
	/// 무늬 목록. 세 가족
	/// - Fade: 단색 막 투명도
	/// - 칸 (Diamond, Dots, Pixels, Blinds): 요소 격자가 순서대로 커지며 덮음
	/// - 무늬 지도 (Iris 이후): 화면 칸마다 덮일 순서 값을 계산해 두고 진행에 따라 경계를 밀며, 경계에 빛
	/// </summary>
	public static class WipeRenderers
	{
		private static ScreenWipeStyle[] all;
		private static ScreenWipeStyle[] patterned;

		/// <summary>Random 과 Fade 를 뺀 무늬들. 빈 후보 목록의 기본값</summary>
		private static ScreenWipeStyle[] Patterned => patterned ??= System.Array.FindAll(All, wipeStyle => wipeStyle != ScreenWipeStyle.Fade);

		/// <summary>Random 을 뺀 전부</summary>
		public static ScreenWipeStyle[] All
		{
			get
			{
				if (all != null)
					return all;
				List<ScreenWipeStyle> styles = new();
				foreach (ScreenWipeStyle wipeStyle in Enum.GetValues(typeof(ScreenWipeStyle)))
				{
					if (wipeStyle != ScreenWipeStyle.Random)
						styles.Add(wipeStyle);
				}
				all = styles.ToArray();
				return all;
			}
		}

		/// <summary>Random 이면 후보 중 하나, 아니면 그대로</summary>
		public static ScreenWipeStyle Resolve(ScreenWipeStyle wipeStyle)
		{
			if (wipeStyle != ScreenWipeStyle.Random)
				return wipeStyle;
			ScreenWipeStyle[] pool = UIFxSettings.Instance.randomPool;
			if (pool == null || pool.Length == 0)
				pool = Patterned;
			ScreenWipeStyle picked = pool[UnityEngine.Random.Range(0, pool.Length)];
			Debug.Assert(picked != ScreenWipeStyle.Random, "UIFxSettings.randomPool 에 Random 이 들어 있음");
			return picked == ScreenWipeStyle.Random ? Patterned[0] : picked;
		}

		public static IWipeRenderer Create(ScreenWipeStyle wipeStyle) => wipeStyle switch
		{
			ScreenWipeStyle.Fade => new FadeWipe(),
			ScreenWipeStyle.Diamond => new TileWipe(TileWipe.Shape.Diamond, TileWipe.Diagonal, true),
			ScreenWipeStyle.Dots => new TileWipe(TileWipe.Shape.Circle, TileWipe.CenterOut, true),
			ScreenWipeStyle.Pixels => new TileWipe(TileWipe.Shape.Square, TileWipe.Scattered, false),
			ScreenWipeStyle.Blinds => new TileWipe(TileWipe.Shape.Row, TileWipe.TopDown, false),
			ScreenWipeStyle.Iris => new MapWipe(WipePatterns.Iris),
			ScreenWipeStyle.Gate => new MapWipe(WipePatterns.Gate),
			ScreenWipeStyle.Clock => new MapWipe(WipePatterns.Clock),
			ScreenWipeStyle.Spiral => new MapWipe(WipePatterns.Spiral),
			ScreenWipeStyle.Dissolve => new MapWipe(WipePatterns.Dissolve),
			ScreenWipeStyle.Wave => new MapWipe(WipePatterns.Wave),
			ScreenWipeStyle.Star => new MapWipe(WipePatterns.Star),
			ScreenWipeStyle.Heart => new MapWipe(WipePatterns.Heart),
			ScreenWipeStyle.Crystal => new MapWipe(WipePatterns.Crystal),
			ScreenWipeStyle.Fan => new MapWipe(WipePatterns.Fan),
			ScreenWipeStyle.Ripple => new MapWipe(WipePatterns.Ripple),
			ScreenWipeStyle.Stripes => new MapWipe(WipePatterns.Stripes),
			_ => throw new ArgumentOutOfRangeException(nameof(wipeStyle), wipeStyle, "WipeRenderers.Create 에 등록 안 된 무늬"),
		};

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
		private VisualElement fill;

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
			Color color = UIFxSettings.Instance.wipeColor;
			color.a *= cover ? eased : 1f - eased;
			fill.style.backgroundColor = color;
		}
	}

	internal sealed class TileWipe : IWipeRenderer
	{
		public enum Shape
		{
			Square = 0,
			Diamond = 1,
			Circle = 2,
			Row = 3,
		}

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

		private readonly Shape shape;
		private readonly Order order;
		private readonly bool overshoot;
		private readonly List<VisualElement> tiles = new();
		private readonly List<float> tileOrder = new();
		private VisualElement container;

		public VisualElement Root => container;

		public TileWipe(Shape shape, Order order, bool overshoot)
		{
			this.shape = shape;
			this.order = order;
			this.overshoot = overshoot;
		}

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
			UIFxSettings settings = UIFxSettings.Instance;
			float cell = Mathf.Max(MIN_CELL_PIXELS, settings.wipeCellSize);
			bool row = shape == Shape.Row;
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
					if (shape == Shape.Diamond)
						tile.style.rotate = new UnityEngine.UIElements.Rotate(new UnityEngine.UIElements.Angle(DIAMOND_ROTATION_DEGREES, AngleUnit.Degree));
					if (shape == Shape.Circle)
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
			UIFxSettings settings = UIFxSettings.Instance;
			float spread = settings.wipeSpread;
			float growth = shape == Shape.Square || shape == Shape.Row ? SQUARE_COVER_GROWTH : settings.wipeTileGrowth;
			for (int i = 0; i < tiles.Count; i++)
			{
				float local = Mathf.Clamp01(progress * (1f + spread) - tileOrder[i] * spread);
				float grown;
				// 픽셀 디졸브: 칸이 한 번에 켜지고 꺼짐. 줄어드는 칸 틈이 격자선처럼 보여서
				if (shape == Shape.Square)
					grown = (local >= 0.5f) == cover ? 1f : 0f;
				else
					grown = cover ? (overshoot ? EaseOutBack(local) : EaseOutCubic(local)) : 1f - local * local;
				float scale = Mathf.Max(0f, grown) * growth;
				tiles[i].style.scale = shape == Shape.Row ? new Scale(new Vector3(1f, scale, 1f)) : new Scale(new Vector3(scale, scale, 1f));
				tiles[i].style.backgroundColor = settings.wipeColor;
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
