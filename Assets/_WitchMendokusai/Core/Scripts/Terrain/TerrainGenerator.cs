using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 지형 샘플링 + 미리보기 텍스처 생성. 에디터/런타임 공통.
	/// 시드 기반 결정적 — 같은 (parameters, x, z) 입력 → 같은 결과.
	/// </summary>
	public static class TerrainGenerator
	{
		// Perlin이 음수 좌표에서 미러링되는 것을 피하기 위한 큰 양수 오프셋
		private const float COORD_OFFSET = 100000f;

		public static float SampleHeight(TerrainParameters parameters, int x, int z)
		{
			float total = 0f;
			float maxValue = 0f;
			float curAmplitude = 1f;
			float curFrequency = parameters.Frequency;

			float seedOffsetX = (parameters.Seed * 0.7341f) % 10000f;
			float seedOffsetZ = (parameters.Seed * 1.2917f) % 10000f;

			for (int i = 0; i < parameters.Octaves; i++)
			{
				float sampleX = (x + COORD_OFFSET + seedOffsetX) * curFrequency;
				float sampleZ = (z + COORD_OFFSET + seedOffsetZ) * curFrequency;
				float perlin = Mathf.PerlinNoise(sampleX, sampleZ) * 2f - 1f;

				total += perlin * curAmplitude;
				maxValue += curAmplitude;

				curAmplitude *= parameters.Persistence;
				curFrequency *= parameters.Lacunarity;
			}

			float normalized = total / maxValue;
			return normalized * parameters.Amplitude;
		}

		public static BiomeData SampleBiome(TerrainParameters parameters, int x, int z)
		{
			if (parameters.Biomes == null || parameters.Biomes.Count == 0)
				return null;

			float seedOffsetX = (parameters.Seed * 2.7314f) % 10000f + 50000f;
			float seedOffsetZ = (parameters.Seed * 0.4129f) % 10000f + 50000f;

			float sampleX = (x + COORD_OFFSET + seedOffsetX) * parameters.BiomeFrequency;
			float sampleZ = (z + COORD_OFFSET + seedOffsetZ) * parameters.BiomeFrequency;
			float noise = Mathf.PerlinNoise(sampleX, sampleZ);

			float totalWeight = 0f;
			for (int i = 0; i < parameters.Biomes.Count; i++)
			{
				if (parameters.Biomes[i].Biome == null)
					continue;
				totalWeight += parameters.Biomes[i].Weight;
			}

			if (totalWeight <= 0f)
				return null;

			float target = noise * totalWeight;
			float accum = 0f;
			for (int i = 0; i < parameters.Biomes.Count; i++)
			{
				BiomeWeight biomeWeight = parameters.Biomes[i];
				if (biomeWeight.Biome == null)
					continue;
				accum += biomeWeight.Weight;
				if (target <= accum)
					return biomeWeight.Biome;
			}

			// fallback: 마지막 valid biome
			for (int i = parameters.Biomes.Count - 1; i >= 0; i--)
			{
				if (parameters.Biomes[i].Biome != null)
					return parameters.Biomes[i].Biome;
			}
			return null;
		}

		public static Texture2D GenerateBiomeTexture(TerrainParameters parameters, int width, int height)
		{
			Texture2D texture = new(width, height, TextureFormat.RGBA32, false)
			{
				filterMode = FilterMode.Point,
				wrapMode = TextureWrapMode.Clamp,
			};

			Color[] pixels = new Color[width * height];
			Color fallback = new(0.4f, 0.4f, 0.4f, 1f);

			for (int z = 0; z < height; z++)
			{
				for (int x = 0; x < width; x++)
				{
					BiomeData biome = SampleBiome(parameters, x, z);
					pixels[z * width + x] = biome != null ? biome.PreviewColor : fallback;
				}
			}

			texture.SetPixels(pixels);
			texture.Apply();
			return texture;
		}

		public static Texture2D GenerateHeightmapTexture(TerrainParameters parameters, int width, int height)
		{
			Texture2D texture = new(width, height, TextureFormat.RGBA32, false)
			{
				filterMode = FilterMode.Point,
				wrapMode = TextureWrapMode.Clamp,
			};

			Color[] pixels = new Color[width * height];

			float minHeight = float.MaxValue;
			float maxHeight = float.MinValue;
			float[] heights = new float[width * height];

			for (int z = 0; z < height; z++)
			{
				for (int x = 0; x < width; x++)
				{
					float h = SampleHeight(parameters, x, z);
					heights[z * width + x] = h;
					if (h < minHeight) minHeight = h;
					if (h > maxHeight) maxHeight = h;
				}
			}

			float range = maxHeight - minHeight;
			if (range < 0.0001f)
				range = 1f;

			for (int i = 0; i < heights.Length; i++)
			{
				float t = (heights[i] - minHeight) / range;
				pixels[i] = new Color(t, t, t, 1f);
			}

			texture.SetPixels(pixels);
			texture.Apply();
			return texture;
		}
	}
}
