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
			// 우선순위: TerrainGraph > heightmap PNG > Perlin (fallback).
			// 그래프 있으면 그래프 실행 결과 — TASK-WM-032 D 부터 가능.
			if (parameters.HasTerrainGraph)
				return parameters.TerrainGraph.SampleHeight(x, z);

			// heightmap PNG 캐시 있으면 그쪽 우선 (외부 툴 import escape hatch). null 캐시 = Perlin path.
			if (parameters.HasHeightmapCache)
			{
				float grayscale = parameters.SampleHeightmapCache(x, z);
				return (grayscale * 2f - 1f) * parameters.Amplitude;
			}

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

		/// <summary>
		/// 한 영역을 heightmap surface mesh로 생성. (size+1)^2 vertices.
		/// 에디터 미리보기용 빠른 surface 표현 — 본격 voxel 메시와 별개.
		/// `chunkX/chunkZ` 는 worldOrigin 청크 좌표 (worldOrigin = chunk * size). size 는 실제 grid 길이.
		/// 바이옴 색상은 vertex color, atlas tileRect 는 sentinel(0,0,0,1) — `WM/VoxelVertexColor` 셰이더가 vertex color path 로 fallback.
		/// </summary>
		public static Mesh GenerateChunkMesh(TerrainParameters parameters, int chunkX, int chunkZ, int size = 16)
		{
			int gridSize = size + 1;
			int vertexCount = gridSize * gridSize;

			Vector3[] vertices = new Vector3[vertexCount];
			Color[] colors = new Color[vertexCount];
			Vector2[] uvs = new Vector2[vertexCount];
			Vector4[] tileRects = new Vector4[vertexCount];
			int[] triangles = new int[size * size * 6];

			int worldOriginX = chunkX * size;
			int worldOriginZ = chunkZ * size;

			// 셰이더가 atlas size=0 = sentinel (vertex color path). worldScale=1 = divide-by-zero 방지.
			Vector4 sentinelTileRect = new(0f, 0f, 0f, 1f);

			for (int z = 0; z < gridSize; z++)
			{
				for (int x = 0; x < gridSize; x++)
				{
					int worldX = worldOriginX + x;
					int worldZ = worldOriginZ + z;

					float height = SampleHeight(parameters, worldX, worldZ);
					BiomeData biome = SampleBiome(parameters, worldX, worldZ);

					int idx = z * gridSize + x;
					vertices[idx] = new Vector3(x, height, z);
					colors[idx] = biome != null ? biome.PreviewColor : new Color(0.5f, 0.5f, 0.5f, 1f);
					uvs[idx] = new Vector2((float)x / size, (float)z / size);
					tileRects[idx] = sentinelTileRect;
				}
			}

			int triIdx = 0;
			for (int z = 0; z < size; z++)
			{
				for (int x = 0; x < size; x++)
				{
					int i = z * gridSize + x;
					triangles[triIdx++] = i;
					triangles[triIdx++] = i + gridSize;
					triangles[triIdx++] = i + 1;
					triangles[triIdx++] = i + 1;
					triangles[triIdx++] = i + gridSize;
					triangles[triIdx++] = i + gridSize + 1;
				}
			}

			Mesh mesh = new() { name = $"ChunkPreview_{chunkX}_{chunkZ}" };
			// size 큰 미리보기 (예: 256+) 의 vertex 수 65535 초과 가능 — UInt32 강제.
			mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
			mesh.vertices = vertices;
			mesh.triangles = triangles;
			mesh.colors = colors;
			mesh.uv = uvs;
			mesh.SetUVs(1, tileRects);
			mesh.RecalculateNormals();
			mesh.RecalculateBounds();
			return mesh;
		}

		/// <summary>
		/// central difference 로 경사 크기 계산 (gradient magnitude). 단위: m/m.
		/// </summary>
		public static float SampleSlope(TerrainParameters parameters, int x, int z)
		{
			float dx = (SampleHeight(parameters, x + 1, z) - SampleHeight(parameters, x - 1, z)) * 0.5f;
			float dz = (SampleHeight(parameters, x, z + 1) - SampleHeight(parameters, x, z - 1)) * 0.5f;
			return Mathf.Sqrt(dx * dx + dz * dz);
		}

		/// <summary>
		/// 경사 열지도 텍스쳐. flat=파랑(H:0.667), steep=빨강(H:0). 전체 최대 경사 기준 정규화.
		/// </summary>
		public static Texture2D GenerateSlopeTexture(TerrainParameters parameters, int width, int height)
		{
			Texture2D texture = new(width, height, TextureFormat.RGBA32, false)
			{
				filterMode = FilterMode.Point,
				wrapMode = TextureWrapMode.Clamp,
			};

			float[] slopes = new float[width * height];
			float maxSlope = 0f;

			for (int z = 0; z < height; z++)
			{
				for (int x = 0; x < width; x++)
				{
					float slope = SampleSlope(parameters, x, z);
					slopes[z * width + x] = slope;
					if (slope > maxSlope)
						maxSlope = slope;
				}
			}

			if (maxSlope < 0.0001f)
				maxSlope = 1f;

			Color[] pixels = new Color[width * height];
			for (int i = 0; i < slopes.Length; i++)
			{
				float t = slopes[i] / maxSlope;
				// flat(t=0)=파랑(H=0.667) → steep(t=1)=빨강(H=0)
				pixels[i] = Color.HSVToRGB((1f - t) * 0.667f, 1f, 1f);
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
