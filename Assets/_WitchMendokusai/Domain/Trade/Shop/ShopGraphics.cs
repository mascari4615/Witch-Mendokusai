using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	// 세로 그라데이션 패널. USS 사용자 속성으로 색 지정
	public class ShopGradientPanel : VisualElement
	{
		private static readonly CustomStyleProperty<Color> TOP_COLOR = new("--wm-gradient-top");
		private static readonly CustomStyleProperty<Color> BOTTOM_COLOR = new("--wm-gradient-bottom");

		private Color topColor;
		private Color bottomColor;

		public ShopGradientPanel()
		{
			generateVisualContent += Draw;
			RegisterCallback<CustomStyleResolvedEvent>(OnStyleResolved);
		}

		private void OnStyleResolved(CustomStyleResolvedEvent evt)
		{
			evt.customStyle.TryGetValue(TOP_COLOR, out topColor);
			evt.customStyle.TryGetValue(BOTTOM_COLOR, out bottomColor);
			MarkDirtyRepaint();
		}

		private void Draw(MeshGenerationContext context)
		{
			float left = resolvedStyle.borderLeftWidth;
			float top = resolvedStyle.borderTopWidth;
			Rect rect = new(left, top, layout.width - left - resolvedStyle.borderRightWidth, layout.height - top - resolvedStyle.borderBottomWidth);
			if (rect.width <= 0 || rect.height <= 0)
				return;

			MeshWriteData mesh = context.Allocate(4, 6);
			mesh.SetNextVertex(new Vertex { position = new Vector3(rect.xMin, rect.yMin, Vertex.nearZ), tint = topColor });
			mesh.SetNextVertex(new Vertex { position = new Vector3(rect.xMax, rect.yMin, Vertex.nearZ), tint = topColor });
			mesh.SetNextVertex(new Vertex { position = new Vector3(rect.xMax, rect.yMax, Vertex.nearZ), tint = bottomColor });
			mesh.SetNextVertex(new Vertex { position = new Vector3(rect.xMin, rect.yMax, Vertex.nearZ), tint = bottomColor });
			mesh.SetAllIndices(new ushort[] { 0, 1, 2, 0, 2, 3 });
		}
	}

	// 외곽선을 글자 뒤에만 그리는 글자. 윤곽이 글자 살을 먹지 않게 앞 글자를 겹침
	public class ShopOutlinedLabel : VisualElement
	{
		private readonly Label back;
		private readonly Label front;

		public ShopOutlinedLabel()
		{
			pickingMode = PickingMode.Ignore;
			back = MakeLayer("wm-shop__outline-back");
			front = MakeLayer("wm-shop__outline-front");
		}

		public string text
		{
			get => back.text;
			set
			{
				back.text = value;
				front.text = value;
			}
		}

		private Label MakeLayer(string layerClass)
		{
			Label layer = new();
			layer.AddToClassList("wm-shop__label");
			layer.AddToClassList(layerClass);
			layer.pickingMode = PickingMode.Ignore;
			Add(layer);
			return layer;
		}
	}

	// 평행사변형 띠, 안쪽 무늬 타일 포함
	public class ShopSlantFill : VisualElement
	{
		private static readonly CustomStyleProperty<float> SLANT = new("--wm-slant");
		private static readonly CustomStyleProperty<Color> FILL_COLOR = new("--wm-slant-fill");
		private static readonly CustomStyleProperty<Sprite> TILE = new("--wm-slant-tile");
		private static readonly CustomStyleProperty<float> TILE_SIZE = new("--wm-slant-tile-size");
		private static readonly CustomStyleProperty<float> TILE_OPACITY = new("--wm-slant-tile-opacity");

		private readonly List<Vector2> polygonBuffer = new();
		private readonly List<Vector2> clipBuffer = new();

		private float slant;
		private Color fillColor;
		private Texture2D tile;
		private float tileSize;
		private float tileOpacity;

		public ShopSlantFill()
		{
			pickingMode = PickingMode.Ignore;
			generateVisualContent += Draw;
			RegisterCallback<CustomStyleResolvedEvent>(OnStyleResolved);
		}

		private void OnStyleResolved(CustomStyleResolvedEvent evt)
		{
			ICustomStyle style = evt.customStyle;
			style.TryGetValue(SLANT, out slant);
			style.TryGetValue(FILL_COLOR, out fillColor);
			tile = style.TryGetValue(TILE, out Sprite sprite) ? sprite.texture : null;
			style.TryGetValue(TILE_SIZE, out tileSize);
			style.TryGetValue(TILE_OPACITY, out tileOpacity);
			MarkDirtyRepaint();
		}

		private void Draw(MeshGenerationContext context)
		{
			float width = layout.width;
			float height = layout.height;
			if (width <= 0 || height <= 0)
				return;

			Vector2 topLeft = new(slant, 0);
			Vector2 topRight = new(width, 0);
			Vector2 bottomRight = new(width - slant, height);
			Vector2 bottomLeft = new(0, height);

			polygonBuffer.Clear();
			polygonBuffer.Add(topLeft);
			polygonBuffer.Add(topRight);
			polygonBuffer.Add(bottomRight);
			polygonBuffer.Add(bottomLeft);
			DrawSolid(context, polygonBuffer, fillColor);

			if (tile == null || tileSize <= 0 || tileOpacity <= 0)
				return;

			Color tint = new(1, 1, 1, tileOpacity);
			for (float y = 0; y < height; y += tileSize)
			{
				for (float x = 0; x < width; x += tileSize)
				{
					polygonBuffer.Clear();
					polygonBuffer.Add(new Vector2(x, y));
					polygonBuffer.Add(new Vector2(Mathf.Min(x + tileSize, width), y));
					polygonBuffer.Add(new Vector2(Mathf.Min(x + tileSize, width), Mathf.Min(y + tileSize, height)));
					polygonBuffer.Add(new Vector2(x, Mathf.Min(y + tileSize, height)));
					ShopPolygon.ClipByHalfPlane(polygonBuffer, clipBuffer, bottomLeft, topLeft);
					ShopPolygon.ClipByHalfPlane(polygonBuffer, clipBuffer, topRight, bottomRight);
					if (polygonBuffer.Count >= 3)
						DrawTile(context, polygonBuffer, new Vector2(x, y), tint);
				}
			}
		}

		private void DrawSolid(MeshGenerationContext context, List<Vector2> polygon, Color color)
		{
			MeshWriteData mesh = context.Allocate(polygon.Count, (polygon.Count - 2) * 3);
			foreach (Vector2 point in polygon)
				mesh.SetNextVertex(new Vertex { position = new Vector3(point.x, point.y, Vertex.nearZ), tint = color });
			ShopPolygon.WriteFan(mesh, polygon.Count);
		}

		private void DrawTile(MeshGenerationContext context, List<Vector2> polygon, Vector2 tileOrigin, Color color)
		{
			MeshWriteData mesh = context.Allocate(polygon.Count, (polygon.Count - 2) * 3, tile);
			foreach (Vector2 point in polygon)
			{
				Vector2 local = (point - tileOrigin) / tileSize;
				mesh.SetNextVertex(new Vertex { position = new Vector3(point.x, point.y, Vertex.nearZ), tint = color, uv = new Vector2(local.x, 1 - local.y) });
			}
			ShopPolygon.WriteFan(mesh, polygon.Count);
		}
	}

	// 아이콘 없는 칸의 사선 무늬와 점선 테두리
	public class ShopEmptyIcon : VisualElement
	{
		private static readonly CustomStyleProperty<Color> STRIPE_A = new("--wm-stripe-a");
		private static readonly CustomStyleProperty<Color> STRIPE_B = new("--wm-stripe-b");
		private static readonly CustomStyleProperty<float> STRIPE_WIDTH = new("--wm-stripe-width");
		private static readonly CustomStyleProperty<Color> DASH_COLOR = new("--wm-dash-color");
		private static readonly CustomStyleProperty<float> DASH_WIDTH = new("--wm-dash-width");
		private static readonly CustomStyleProperty<float> DASH_LENGTH = new("--wm-dash-length");

		private const float DIAGONAL = 1.41421356f;

		private readonly List<Vector2> polygonBuffer = new();
		private readonly List<Vector2> clipBuffer = new();

		private Color stripeA;
		private Color stripeB;
		private float stripeWidth;
		private Color dashColor;
		private float dashWidth;
		private float dashLength;

		public ShopEmptyIcon()
		{
			pickingMode = PickingMode.Ignore;
			generateVisualContent += Draw;
			RegisterCallback<CustomStyleResolvedEvent>(OnStyleResolved);
		}

		private void OnStyleResolved(CustomStyleResolvedEvent evt)
		{
			ICustomStyle style = evt.customStyle;
			style.TryGetValue(STRIPE_A, out stripeA);
			style.TryGetValue(STRIPE_B, out stripeB);
			style.TryGetValue(STRIPE_WIDTH, out stripeWidth);
			style.TryGetValue(DASH_COLOR, out dashColor);
			style.TryGetValue(DASH_WIDTH, out dashWidth);
			style.TryGetValue(DASH_LENGTH, out dashLength);
			MarkDirtyRepaint();
		}

		private void Draw(MeshGenerationContext context)
		{
			float width = layout.width;
			float height = layout.height;
			if (width <= 0 || height <= 0 || stripeWidth <= 0 || dashLength <= 0)
				return;

			DrawStripes(context, width, height);
			DrawDashes(context, width, height);
		}

		private void DrawStripes(MeshGenerationContext context, float width, float height)
		{
			float step = stripeWidth * DIAGONAL;
			int count = Mathf.CeilToInt((width + height) / step);
			for (int i = 0; i < count; i++)
			{
				float near = i * step;
				float far = near + step;
				polygonBuffer.Clear();
				polygonBuffer.Add(new Vector2(width - far, 0));
				polygonBuffer.Add(new Vector2(width - near, 0));
				polygonBuffer.Add(new Vector2(width - near + height, height));
				polygonBuffer.Add(new Vector2(width - far + height, height));
				ShopPolygon.ClipByHalfPlane(polygonBuffer, clipBuffer, new Vector2(0, height), Vector2.zero);
				ShopPolygon.ClipByHalfPlane(polygonBuffer, clipBuffer, new Vector2(width, 0), new Vector2(width, height));
				if (polygonBuffer.Count < 3)
					continue;

				MeshWriteData mesh = context.Allocate(polygonBuffer.Count, (polygonBuffer.Count - 2) * 3);
				Color color = i % 2 == 0 ? stripeA : stripeB;
				foreach (Vector2 point in polygonBuffer)
					mesh.SetNextVertex(new Vertex { position = new Vector3(point.x, point.y, Vertex.nearZ), tint = color });
				ShopPolygon.WriteFan(mesh, polygonBuffer.Count);
			}
		}

		private void DrawDashes(MeshGenerationContext context, float width, float height)
		{
			Painter2D painter = context.painter2D;
			painter.fillColor = dashColor;
			for (float x = 0; x < width; x += dashLength * 2)
			{
				float length = Mathf.Min(dashLength, width - x);
				FillRect(painter, new Rect(x, 0, length, dashWidth));
				FillRect(painter, new Rect(x, height - dashWidth, length, dashWidth));
			}
			for (float y = 0; y < height; y += dashLength * 2)
			{
				float length = Mathf.Min(dashLength, height - y);
				FillRect(painter, new Rect(0, y, dashWidth, length));
				FillRect(painter, new Rect(width - dashWidth, y, dashWidth, length));
			}
		}

		private static void FillRect(Painter2D painter, Rect rect)
		{
			painter.BeginPath();
			painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
			painter.LineTo(new Vector2(rect.xMax, rect.yMin));
			painter.LineTo(new Vector2(rect.xMax, rect.yMax));
			painter.LineTo(new Vector2(rect.xMin, rect.yMax));
			painter.ClosePath();
			painter.Fill();
		}
	}

	internal static class ShopPolygon
	{
		// 시작점에서 끝점 방향의 오른쪽 면만 유지
		public static void ClipByHalfPlane(List<Vector2> polygon, List<Vector2> buffer, Vector2 start, Vector2 end)
		{
			buffer.Clear();
			Vector2 edge = end - start;
			for (int i = 0; i < polygon.Count; i++)
			{
				Vector2 current = polygon[i];
				Vector2 next = polygon[(i + 1) % polygon.Count];
				float currentSide = edge.x * (current.y - start.y) - edge.y * (current.x - start.x);
				float nextSide = edge.x * (next.y - start.y) - edge.y * (next.x - start.x);
				if (currentSide >= 0)
					buffer.Add(current);
				if ((currentSide >= 0) != (nextSide >= 0))
					buffer.Add(Vector2.Lerp(current, next, currentSide / (currentSide - nextSide)));
			}
			polygon.Clear();
			polygon.AddRange(buffer);
		}

		public static void WriteFan(MeshWriteData mesh, int vertexCount)
		{
			for (int i = 1; i < vertexCount - 1; i++)
			{
				mesh.SetNextIndex(0);
				mesh.SetNextIndex((ushort)i);
				mesh.SetNextIndex((ushort)(i + 1));
			}
		}
	}
}
