using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	// 가운데 없는 9분할 틀. 픽셀 폭을 USS 값 그대로 사용
	public class ShopNineSlice : VisualElement
	{
		private static readonly CustomStyleProperty<Sprite> IMAGE = new("--wm-nine-image");
		private static readonly CustomStyleProperty<float> SLICE = new("--wm-nine-slice");
		private static readonly CustomStyleProperty<float> WIDTH = new("--wm-nine-width");

		private Texture2D image;
		private float slice;
		private float width;

		public ShopNineSlice()
		{
			pickingMode = PickingMode.Ignore;
			generateVisualContent += Draw;
			RegisterCallback<CustomStyleResolvedEvent>(OnStyleResolved);
		}

		private void OnStyleResolved(CustomStyleResolvedEvent evt)
		{
			ICustomStyle style = evt.customStyle;
			image = style.TryGetValue(IMAGE, out Sprite sprite) ? sprite.texture : null;
			style.TryGetValue(SLICE, out slice);
			style.TryGetValue(WIDTH, out width);
			MarkDirtyRepaint();
		}

		private void Draw(MeshGenerationContext context)
		{
			if (image == null || slice <= 0 || width <= 0 || layout.width <= 0 || layout.height <= 0)
				return;

			float[] xs = { 0, width, layout.width - width, layout.width };
			float[] ys = { 0, width, layout.height - width, layout.height };
			float uvSliceX = slice / image.width;
			float uvSliceY = slice / image.height;
			float[] us = { 0, uvSliceX, 1 - uvSliceX, 1 };
			float[] vs = { 1, 1 - uvSliceY, uvSliceY, 0 };

			for (int row = 0; row < 3; row++)
			{
				for (int column = 0; column < 3; column++)
				{
					if (row == 1 && column == 1)
						continue;

					MeshWriteData mesh = context.Allocate(4, 6, image);
					mesh.SetNextVertex(Corner(xs[column], ys[row], us[column], vs[row]));
					mesh.SetNextVertex(Corner(xs[column + 1], ys[row], us[column + 1], vs[row]));
					mesh.SetNextVertex(Corner(xs[column + 1], ys[row + 1], us[column + 1], vs[row + 1]));
					mesh.SetNextVertex(Corner(xs[column], ys[row + 1], us[column], vs[row + 1]));
					ShopPolygon.WriteFan(mesh, 4);
				}
			}
		}

		private static Vertex Corner(float x, float y, float u, float v) => new()
		{
			position = new Vector3(x, y, Vertex.nearZ),
			tint = Color.white,
			uv = new Vector2(u, v),
		};
	}
}
