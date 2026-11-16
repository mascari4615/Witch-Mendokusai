using UnityEngine;

namespace WitchMendokusai
{
	// UI 연출용 계산 텍스처. 외부 그림 없이 수식으로 한 번 만들어 재사용
	public static class UIFxTextures
	{
		private const int GLOW_PIXELS = 64;
		private const int SPARKLE_PIXELS = 64;
		private const int BAND_PIXELS = 128;
		// 반짝이 십자 빛줄기의 가늘기. 클수록 가늚
		private const float SPARKLE_RAY_SHARPNESS = 10f;

		private static Texture2D glow;
		private static Texture2D sparkle;
		private static Texture2D band;

		/// <summary>가운데가 밝고 가장자리로 사라지는 둥근 빛</summary>
		public static Texture2D Glow => glow != null ? glow : (glow = Make(GLOW_PIXELS, GLOW_PIXELS, (horizontal, vertical) =>
		{
			float distance = Mathf.Sqrt(horizontal * horizontal + vertical * vertical);
			float falloff = Mathf.Clamp01(1f - distance);
			return falloff * falloff;
		}));

		/// <summary>둥근 빛에 가로세로 빛줄기를 더한 반짝임</summary>
		public static Texture2D Sparkle => sparkle != null ? sparkle : (sparkle = Make(SPARKLE_PIXELS, SPARKLE_PIXELS, (horizontal, vertical) =>
		{
			float distance = Mathf.Sqrt(horizontal * horizontal + vertical * vertical);
			float core = Mathf.Pow(Mathf.Clamp01(1f - distance * 2.2f), 2f);
			float rayX = Mathf.Exp(-Mathf.Abs(vertical) * SPARKLE_RAY_SHARPNESS * 2f) * Mathf.Clamp01(1f - Mathf.Abs(horizontal));
			float rayY = Mathf.Exp(-Mathf.Abs(horizontal) * SPARKLE_RAY_SHARPNESS * 2f) * Mathf.Clamp01(1f - Mathf.Abs(vertical));
			return Mathf.Clamp01(core + (rayX + rayY) * 0.8f);
		}));

		/// <summary>가로 방향으로 가운데만 밝은 빛 띠</summary>
		public static Texture2D Band => band != null ? band : (band = Make(BAND_PIXELS, 1, (horizontal, vertical) =>
		{
			float falloff = Mathf.Clamp01(1f - Mathf.Abs(horizontal));
			return Mathf.SmoothStep(0f, 1f, falloff);
		}));

		// horizontal, vertical 는 -1 ~ 1. 결과는 흰색의 알파
		private static Texture2D Make(int width, int height, System.Func<float, float, float> alphaAt)
		{
			Texture2D texture = new(width, height, TextureFormat.RGBA32, false)
			{
				wrapMode = TextureWrapMode.Clamp,
				filterMode = FilterMode.Bilinear,
				hideFlags = HideFlags.HideAndDontSave,
			};
			Color32[] pixels = new Color32[width * height];
			for (int y = 0; y < height; y++)
			{
				for (int x = 0; x < width; x++)
				{
					float horizontal = width == 1 ? 0f : (x + 0.5f) / width * 2f - 1f;
					float vertical = height == 1 ? 0f : (y + 0.5f) / height * 2f - 1f;
					byte alpha = (byte)Mathf.RoundToInt(alphaAt(horizontal, vertical) * 255f);
					pixels[y * width + x] = new Color32(255, 255, 255, alpha);
				}
			}
			texture.SetPixels32(pixels);
			texture.Apply();
			return texture;
		}
	}
}
