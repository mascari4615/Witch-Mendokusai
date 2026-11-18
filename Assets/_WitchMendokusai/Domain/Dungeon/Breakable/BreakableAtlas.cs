using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 색 하나당 가로 띠 한 줄. 띠 안은 가로로만 결 명암이 흐르는 저해상도 텍스처, 부드러운 필터
	public sealed class BreakableAtlas
	{
		private readonly List<Color> colors = new();
		private readonly int stripPixels;
		private readonly int capacity;

		public BreakableAtlas(int capacity, int stripPixels)
		{
			this.capacity = capacity;
			this.stripPixels = stripPixels;
		}

		private int Height => Mathf.NextPowerOfTwo(capacity * stripPixels);

		public int Add(Color color)
		{
			if (colors.Count >= capacity)
				throw new System.InvalidOperationException($"{nameof(BreakableAtlas)} 줄 수 초과 {capacity}");
			colors.Add(color);
			return colors.Count - 1;
		}

		// 띠 한가운데. 위아래 줄과 섞이지 않음
		public float RowV(int row) => (row * stripPixels + stripPixels * .5f) / Height;

		public Texture2D Build(int grainPixels, float grainStrength, int seed)
		{
			Texture2D texture = new(grainPixels, Height, TextureFormat.RGBA32, false)
			{
				name = "BreakableAtlas",
				filterMode = FilterMode.Bilinear,
				wrapModeU = TextureWrapMode.Repeat,
				wrapModeV = TextureWrapMode.Clamp,
				hideFlags = HideFlags.DontSave,
			};
			Color[] pixels = new Color[grainPixels * Height];
			System.Random random = new(seed);
			for (int row = 0; row < colors.Count; row++)
			{
				float[] grain = BuildGrain(grainPixels, random);
				for (int x = 0; x < grainPixels; x++)
				{
					float shade = 1f + grain[x] * grainStrength;
					Color color = colors[row];
					Color shaded = new(color.r * shade, color.g * shade, color.b * shade, 1f);
					for (int y = 0; y < stripPixels; y++)
						pixels[(row * stripPixels + y) * grainPixels + x] = shaded;
				}
			}
			texture.SetPixels(pixels);
			texture.Apply(false, true);
			return texture;
		}

		// 굵은 결 둘과 잔결 하나를 겹친 -1..1 명암, 가로 반복 이음새 없음
		private static float[] BuildGrain(int width, System.Random random)
		{
			float[] grain = new float[width];
			float phaseA = (float)random.NextDouble() * Mathf.PI * 2f;
			float phaseB = (float)random.NextDouble() * Mathf.PI * 2f;
			float[] jitter = new float[width];
			for (int x = 0; x < width; x++)
				jitter[x] = (float)random.NextDouble() * 2f - 1f;
			for (int x = 0; x < width; x++)
			{
				float angle = x * Mathf.PI * 2f / width;
				float smooth = (jitter[x] * .5f + jitter[(x + 1) % width] * .25f + jitter[(x + width - 1) % width] * .25f);
				grain[x] = Mathf.Sin(angle * 2f + phaseA) * .45f + Mathf.Sin(angle * 5f + phaseB) * .25f + smooth * .3f;
			}
			return grain;
		}
	}
}
