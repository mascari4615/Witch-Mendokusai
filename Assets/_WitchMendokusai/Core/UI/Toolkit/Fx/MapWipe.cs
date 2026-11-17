using UnityEngine;
using UnityEngine.UIElements;

namespace WitchMendokusai
{
	/// <summary>
	/// 무늬 지도 전환. 화면을 저해상도 칸으로 나눠 칸마다 덮일 순서 값 (0~1) 을 한 번 계산하고,
	/// 매 프레임 진행 값과 비교해 알파와 경계 빛을 칠한 텍스처를 화면에 늘려 그림 (셰이더 디졸브와 같은 원리, CPU 계산)
	/// 무늬는 WipePatterns 의 함수 하나. 칸 수와 경계는 UIFxSettings
	/// </summary>
	internal sealed class MapWipe : IWipeRenderer
	{
		// 계산 해상도 범위. 한 프레임 칸 수가 커지지 않게 묶음
		private const int MIN_MAP_WIDTH = 16;
		private const int MAX_MAP_WIDTH = 512;
		private const int MAX_MAP_ROWS = 512;
		// 4 x (1 - x) 의 최댓값은 x 가 0.5 일 때 1. 경계 한가운데가 가장 밝게
		private const float EDGE_PEAK_NORMALIZER = 4f;

		private readonly WipePatterns.Pattern pattern;
		private VisualElement image;
		private Texture2D texture;
		private float[] order;
		private Color32[] pixels;

		public MapWipe(WipePatterns.Pattern pattern) => this.pattern = pattern;

		public VisualElement Root => image;

		public void Attach(VisualElement host)
		{
			image = WipeRenderers.MakeFill("wipe-map");
			image.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
			host.Add(image);
		}

		public void Detach()
		{
			image?.RemoveFromHierarchy();
			if (texture != null)
				Object.Destroy(texture);
			texture = null;
			order = null;
		}

		public void Resize(Vector2 size)
		{
			int width = Mathf.Clamp(UIFxSettings.Instance.wipeMapWidth, MIN_MAP_WIDTH, MAX_MAP_WIDTH);
			int height = Mathf.Clamp(Mathf.RoundToInt(width * size.y / size.x), 1, MAX_MAP_ROWS);
			float aspect = size.x / size.y;

			if (texture != null)
				Object.Destroy(texture);
			texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
			{
				wrapMode = TextureWrapMode.Clamp,
				filterMode = FilterMode.Bilinear,
				hideFlags = HideFlags.HideAndDontSave,
			};
			image.style.backgroundImage = new StyleBackground(texture);
			pixels = new Color32[width * height];
			order = new float[width * height];

			float min = float.MaxValue;
			float max = float.MinValue;
			for (int row = 0; row < height; row++)
			{
				// 텍스처 첫 줄은 아래. 무늬 좌표는 위가 0
				float y = 1f - (row + 0.5f) / height;
				for (int column = 0; column < width; column++)
				{
					float x = (column + 0.5f) / width;
					float value = pattern(x, y, aspect);
					order[row * width + column] = value;
					min = Mathf.Min(min, value);
					max = Mathf.Max(max, value);
				}
			}
			float range = Mathf.Max(1e-5f, max - min);
			for (int i = 0; i < order.Length; i++)
				order[i] = (order[i] - min) / range;
		}

		public void Draw(float progress, bool cover)
		{
			if (order == null)
				return;
			UIFxSettings settings = UIFxSettings.Instance;
			float softness = settings.wipeEdgeSoftness;
			float threshold = Mathf.SmoothStep(0f, 1f, progress) * (1f + softness);
			Color baseColor = settings.wipeColor;
			Color edgeColor = settings.wipeEdgeColor;
			float glowStrength = settings.wipeEdgeGlow;

			for (int i = 0; i < order.Length; i++)
			{
				float covered = Mathf.Clamp01((threshold - order[i]) / softness);
				if (cover == false)
					covered = 1f - covered;
				// 경계 (덮임 절반) 에서 가장 밝은 빛
				float glow = EDGE_PEAK_NORMALIZER * covered * (1f - covered) * glowStrength;
				Color color = Color.Lerp(baseColor, edgeColor, glow);
				color.a = Mathf.Max(covered, glow) * baseColor.a;
				pixels[i] = color;
			}
			texture.SetPixels32(pixels);
			texture.Apply(false);
		}
	}
}
