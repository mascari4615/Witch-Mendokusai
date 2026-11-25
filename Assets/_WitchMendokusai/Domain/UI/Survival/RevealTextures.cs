using UnityEngine;

namespace WitchMendokusai
{
	// 공개 연출용 부드러운 그림. 처음 쓸 때 한 번 계산해 둠
	// - 단색 삼각형 빛줄기 대신 가운데만 밝고 가장자리가 빨리 꺼지는 번짐, 흐린 고리, 세로 빛기둥
	public static class RevealTextures
	{
		private const int DOT_SIZE = 64;
		private const int BLOOM_SIZE = 256;
		private const int RING_SIZE = 256;
		private const int BEAM_WIDTH = 64;
		public const int CARD_GLOW_BORDER = 32;
		// 선형 색 공간이라 옅은 알파도 밝게 보임. 빨리 꺼지게
		private const float CARD_GLOW_PEAK = 0.5f;
		private const float CARD_GLOW_FALLOFF = 3.5f;

		private static Sprite softDot;
		private static Sprite bloom;
		private static Sprite ring;
		private static Sprite beam;
		private static Sprite vignette;
		private static Sprite cardGlow;

		public static Sprite SoftDot => softDot != null ? softDot : softDot = Radial(DOT_SIZE, distance => Mathf.Pow(Mathf.Clamp01(1f - distance), 2.2f));
		// 가운데 14% 까지 거의 흰색, 42% 에서 약하게, 끝은 0. 넓게 깔려 화면을 씻지 않게
		public static Sprite Bloom => bloom != null ? bloom : bloom = Radial(BLOOM_SIZE, distance => Mathf.Clamp01(Mathf.Exp(-distance * distance * 9f)));
		// 가운데가 빈 흐린 고리. 바닥 파동
		public static Sprite Ring => ring != null ? ring : ring = Radial(RING_SIZE, distance => Mathf.Clamp01(Mathf.Exp(-Mathf.Pow((distance - 0.72f) / 0.13f, 2f))));
		// 가운데도 절반은 어둡게, 가장자리는 끝까지
		public static Sprite Vignette => vignette != null ? vignette : vignette = Radial(BLOOM_SIZE, distance => Mathf.Lerp(0.55f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(distance * 0.85f))));
		public static Sprite Beam => beam != null ? beam : beam = BuildBeam();

		// 네모를 따라 번지는 빛 (조각 그림). 가장자리 안쪽은 꽉 차고 밖으로 꺼짐
		public static Sprite CardGlow => cardGlow != null ? cardGlow : cardGlow = BuildCardGlow();

		private static Sprite BuildCardGlow()
		{
			const int SIZE = CARD_GLOW_BORDER * 2 + 2;
			Texture2D texture = NewTexture(SIZE, SIZE);
			Color32[] pixels = new Color32[SIZE * SIZE];
			for (int y = 0; y < SIZE; y++)
			{
				for (int x = 0; x < SIZE; x++)
				{
					float across = Mathf.Max(0f, CARD_GLOW_BORDER - Mathf.Min(x, SIZE - 1 - x));
					float along = Mathf.Max(0f, CARD_GLOW_BORDER - Mathf.Min(y, SIZE - 1 - y));
					float away = Mathf.Clamp01(Mathf.Sqrt(across * across + along * along) / CARD_GLOW_BORDER);
					float alpha = CARD_GLOW_PEAK * Mathf.Pow(1f - away, CARD_GLOW_FALLOFF);
					pixels[y * SIZE + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
				}
			}
			texture.SetPixels32(pixels);
			texture.Apply(false, true);
			return Sprite.Create(texture, new Rect(0f, 0f, SIZE, SIZE), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, Vector4.one * CARD_GLOW_BORDER);
		}

		private static Sprite Radial(int size, System.Func<float, float> alphaAtDistance)
		{
			Texture2D texture = NewTexture(size, size);
			Color32[] pixels = new Color32[size * size];
			float half = (size - 1) * 0.5f;
			for (int y = 0; y < size; y++)
			{
				for (int x = 0; x < size; x++)
				{
					float distance = Mathf.Sqrt((x - half) * (x - half) + (y - half) * (y - half)) / half;
					byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(alphaAtDistance(distance)) * 255f);
					pixels[y * size + x] = new Color32(255, 255, 255, alpha);
				}
			}
			texture.SetPixels32(pixels);
			texture.Apply(false, true);
			return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
		}

		// 가로는 가운데 밝은 심과 넓은 번짐, 세로는 위쪽 22% 가 흐려지며 사라짐
		private static Sprite BuildBeam()
		{
			const int BEAM_HEIGHT = 256;
			Texture2D texture = NewTexture(BEAM_WIDTH, BEAM_HEIGHT);
			Color32[] pixels = new Color32[BEAM_WIDTH * BEAM_HEIGHT];
			float half = (BEAM_WIDTH - 1) * 0.5f;
			for (int y = 0; y < BEAM_HEIGHT; y++)
			{
				float vertical = y / (float)(BEAM_HEIGHT - 1);
				float top = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - vertical) / 0.22f));
				for (int x = 0; x < BEAM_WIDTH; x++)
				{
					float across = Mathf.Abs(x - half) / half;
					float core = Mathf.Exp(-across * across * 60f);
					float glow = Mathf.Exp(-across * across * 5f) * 0.55f;
					byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01((core + glow) * top) * 255f);
					pixels[y * BEAM_WIDTH + x] = new Color32(255, 255, 255, alpha);
				}
			}
			texture.SetPixels32(pixels);
			texture.Apply(false, true);
			return Sprite.Create(texture, new Rect(0f, 0f, BEAM_WIDTH, BEAM_HEIGHT), new Vector2(0.5f, 0f));
		}

		private static Texture2D NewTexture(int width, int height) => new(width, height, TextureFormat.RGBA32, false)
		{
			wrapMode = TextureWrapMode.Clamp,
			filterMode = FilterMode.Bilinear,
			hideFlags = HideFlags.DontSave,
		};
	}
}
