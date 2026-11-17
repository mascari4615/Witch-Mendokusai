using System.Collections.Generic;
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
		private const float SPARKLE_RAY_SHARPNESS = 6f;

		private static Texture2D glow;
		private static Texture2D sparkle;
		private static Texture2D band;
		private static readonly Dictionary<ParticleShape, Texture2D> shapes = new();
		private const int SHAPE_PIXELS = 64;

		/// <summary>입자 모양 텍스처. 흰색, 색은 사용처에서 tint</summary>
		public static Texture2D Shape(ParticleShape shape)
		{
			if (shapes.TryGetValue(shape, out Texture2D cached) && cached != null)
				return cached;
			Texture2D texture = shape switch
			{
				ParticleShape.Glow => Glow,
				ParticleShape.Sparkle => Sparkle,
				ParticleShape.Star => Make(SHAPE_PIXELS, SHAPE_PIXELS, StarAlpha),
				ParticleShape.Snow => Make(SHAPE_PIXELS, SHAPE_PIXELS, SnowAlpha),
				ParticleShape.Ring => Make(SHAPE_PIXELS, SHAPE_PIXELS, RingAlpha),
				ParticleShape.Heart => Make(SHAPE_PIXELS, SHAPE_PIXELS, HeartAlpha),
				ParticleShape.Petal => Make(SHAPE_PIXELS, SHAPE_PIXELS, PetalAlpha),
				ParticleShape.Diamond => Make(SHAPE_PIXELS, SHAPE_PIXELS, DiamondAlpha),
				_ => throw new System.ArgumentOutOfRangeException(nameof(shape), shape, "UIFxTextures.Shape 에 등록 안 된 모양"),
			};
			shapes[shape] = texture;
			return texture;
		}

		/// <summary>모양이 둥글어 돌려도 같은지 (회전 생략용)</summary>
		public static bool IsRound(ParticleShape shape) => shape == ParticleShape.Glow || shape == ParticleShape.Ring;

		// 이하 모양 함수. horizontal 오른쪽, vertical 위가 + (텍스처 첫 줄이 아래)
		private static float StarAlpha(float horizontal, float vertical)
		{
			float distance = Mathf.Sqrt(horizontal * horizontal + vertical * vertical);
			float theta = Mathf.Atan2(vertical, horizontal) - Mathf.PI * 0.5f;
			float point = (Mathf.Cos(5f * theta) + 1f) * 0.5f;
			float edge = 0.42f + 0.5f * point * point * point;
			return Mathf.Max(Smooth(edge + 0.02f, edge - 0.1f, distance), Mathf.Clamp01(1f - distance) * 0.25f);
		}

		private static float SnowAlpha(float horizontal, float vertical)
		{
			float distance = Mathf.Sqrt(horizontal * horizontal + vertical * vertical);
			float theta = Mathf.Atan2(vertical, horizontal);
			float ray = Mathf.Exp(-Mathf.Abs(Mathf.Sin(3f * theta)) * distance * 14f) * Smooth(0.95f, 0.5f, distance);
			return Mathf.Max(ray, Smooth(0.28f, 0f, distance));
		}

		private static float RingAlpha(float horizontal, float vertical)
		{
			float distance = Mathf.Sqrt(horizontal * horizontal + vertical * vertical);
			float offset = (distance - 0.6f) / 0.13f;
			return Mathf.Exp(-offset * offset);
		}

		private static float HeartAlpha(float horizontal, float vertical)
		{
			float x = horizontal * 1.3f;
			float y = vertical * 1.35f + 0.1f;
			float squared = x * x + y * y - 1f;
			float curve = squared * squared * squared - x * x * y * y * y;
			return Smooth(0.02f, -0.12f, curve);
		}

		private static float PetalAlpha(float horizontal, float vertical)
		{
			float width = 0.5f * (1f - vertical * vertical);
			float inside = Smooth(0.04f, -0.04f, Mathf.Abs(horizontal) - width);
			float shade = 0.7f + 0.3f * (1f - Mathf.Abs(horizontal) / Mathf.Max(0.05f, width));
			return inside * Mathf.Clamp01(shade);
		}

		private static float DiamondAlpha(float horizontal, float vertical)
		{
			float distance = Mathf.Abs(horizontal) / 0.6f + Mathf.Abs(vertical);
			return Smooth(1f, 0.82f, distance);
		}

		// edge0 에서 0, edge1 에서 1 (edge0 > edge1 이면 반대 방향)
		private static float Smooth(float edge0, float edge1, float value)
		{
			float ratio = Mathf.Clamp01((value - edge0) / (edge1 - edge0));
			return ratio * ratio * (3f - 2f * ratio);
		}

		/// <summary>가운데가 밝고 가장자리로 사라지는 둥근 빛</summary>
		public static Texture2D Glow => glow != null ? glow : (glow = Make(GLOW_PIXELS, GLOW_PIXELS, (horizontal, vertical) =>
		{
			float distance = Mathf.Sqrt(horizontal * horizontal + vertical * vertical);
			float falloff = Mathf.Clamp01(1f - distance);
			// 가운데 밝은 심 + 넓은 번짐
			float core = Mathf.Clamp01(1f - distance * 3f);
			return Mathf.Clamp01(falloff * falloff * 0.6f + core * 0.8f);
		}));

		/// <summary>둥근 빛에 가로세로 빛줄기를 더한 반짝임</summary>
		public static Texture2D Sparkle => sparkle != null ? sparkle : (sparkle = Make(SPARKLE_PIXELS, SPARKLE_PIXELS, (horizontal, vertical) =>
		{
			float distance = Mathf.Sqrt(horizontal * horizontal + vertical * vertical);
			float core = Mathf.Pow(Mathf.Clamp01(1f - distance * 1.6f), 1.5f);
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
