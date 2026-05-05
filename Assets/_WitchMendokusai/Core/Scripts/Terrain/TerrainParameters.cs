using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	[CreateAssetMenu(fileName = nameof(TerrainParameters), menuName = "WM/Terrain/" + nameof(TerrainParameters))]
	public class TerrainParameters : ScriptableObject
	{
		[Header("World")]
		[SerializeField] private int seed = 0;

		[Header("Heightmap")]
		[SerializeField, Range(1, 8)] private int octaves = 4;
		[SerializeField] private float frequency = 0.01f;
		[SerializeField] private float amplitude = 32f;
		[SerializeField, Range(0f, 1f)] private float persistence = 0.5f;
		[SerializeField] private float lacunarity = 2f;

		[Header("Terrain Graph (노드 그래프 — null = heightmap PNG 또는 Perlin 사용)")]
		[Tooltip("WM/Terrain/TerrainGraph SO. WorldPositionInputNode + HeightOutputNode 가 진입/출구. 우선순위 1 — graph 있으면 PNG/Perlin 무시.")]
		[SerializeField] private TerrainGraph terrainGraph;

		[Header("Heightmap Texture (외부 툴 import — null = Perlin 사용)")]
		[Tooltip("World Machine / WorldPainter / 손그림 PNG 등. R 채널 grayscale = [0,1] → ±Amplitude. Inspector 에서 isReadable=true 필수. null 이면 Perlin 노이즈 그대로.")]
		[SerializeField] private Texture2D heightmapTexture;
		[Tooltip("월드 m 당 텍스쳐 픽셀 비율. 1 = 1m/pixel. 작은 값(0.5)이면 텍스쳐가 큰 영역 커버 (각 픽셀 = 2m). 큰 값(2)이면 작은 영역 (각 픽셀 = 0.5m, 디테일).")]
		[SerializeField, Min(0.01f)] private float heightmapWorldScale = 1f;

		[Header("Biome")]
		[SerializeField] private float biomeFrequency = 0.005f;
		[SerializeField] private List<BiomeWeight> biomes = new();

		// background thread 안전 — Color[] 캐시 (main thread 에서 Texture2D.GetPixels 한 번)
		[System.NonSerialized] private float[] heightmapCache;
		[System.NonSerialized] private int heightmapCacheWidth;
		[System.NonSerialized] private int heightmapCacheHeight;
		[System.NonSerialized] private Texture2D cachedTexture;

		public int Seed => seed;
		public int Octaves => octaves;
		public float Frequency => frequency;
		public float Amplitude => amplitude;
		public float Persistence => persistence;
		public float Lacunarity => lacunarity;
		public float BiomeFrequency => biomeFrequency;
		public IReadOnlyList<BiomeWeight> Biomes => biomes;
		public Texture2D HeightmapTexture => heightmapTexture;
		public float HeightmapWorldScale => heightmapWorldScale;
		public bool HasHeightmapCache => heightmapCache != null;
		public TerrainGraph TerrainGraph => terrainGraph;
		public bool HasTerrainGraph => terrainGraph != null;

		public void SetSeed(int value) => seed = value;
		public void SetOctaves(int value) => octaves = Mathf.Clamp(value, 1, 8);
		public void SetFrequency(float value) => frequency = Mathf.Max(0.0001f, value);
		public void SetAmplitude(float value) => amplitude = value;
		public void SetPersistence(float value) => persistence = Mathf.Clamp01(value);
		public void SetLacunarity(float value) => lacunarity = Mathf.Max(1f, value);
		public void SetBiomeFrequency(float value) => biomeFrequency = Mathf.Max(0.0001f, value);

		public void SetBiomes(List<BiomeWeight> value) => biomes = value ?? new();
		public void SetHeightmapTexture(Texture2D value)
		{
			heightmapTexture = value;
			InvalidateHeightmapCache();
		}
		public void SetHeightmapWorldScale(float value) => heightmapWorldScale = Mathf.Max(0.01f, value);

		/// <summary>
		/// **main thread only** — Texture2D.GetPixels 호출. SampleHeightmapCache 는 background thread 안전.
		/// 텍스쳐 변경 시 자동 재캐시. isReadable=false 면 경고 + cache null (Perlin fallback).
		/// </summary>
		public void EnsureHeightmapCache()
		{
			if (heightmapTexture == null)
			{
				InvalidateHeightmapCache();
				return;
			}
			if (cachedTexture == heightmapTexture && heightmapCache != null)
				return;

			if (heightmapTexture.isReadable == false)
			{
				Debug.LogWarning($"[TerrainParameters] heightmapTexture '{heightmapTexture.name}' isReadable=false — Inspector → Texture importer → Read/Write 활성화 필요. Perlin fallback.");
				InvalidateHeightmapCache();
				return;
			}

			Color[] pixels = heightmapTexture.GetPixels();
			heightmapCacheWidth = heightmapTexture.width;
			heightmapCacheHeight = heightmapTexture.height;
			heightmapCache = new float[pixels.Length];
			for (int i = 0; i < pixels.Length; i++)
				heightmapCache[i] = pixels[i].r;
			cachedTexture = heightmapTexture;
			Debug.Log($"[TerrainParameters] heightmap cache: {heightmapTexture.name} ({heightmapCacheWidth}×{heightmapCacheHeight}) — worldScale {heightmapWorldScale} m/pixel");
		}

		public void InvalidateHeightmapCache()
		{
			heightmapCache = null;
			cachedTexture = null;
		}

		/// <summary>
		/// background thread 안전 — pure float[] read. 텍스쳐 중심 = 월드 (0,0).
		/// 0~1 grayscale → SampleHeight 가 ±Amplitude 매핑.
		/// </summary>
		public float SampleHeightmapCache(int worldX, int worldZ)
		{
			if (heightmapCache == null)
				return 0.5f;
			float u = worldX / heightmapWorldScale + heightmapCacheWidth * 0.5f;
			float v = worldZ / heightmapWorldScale + heightmapCacheHeight * 0.5f;
			int px = Mathf.Clamp(Mathf.RoundToInt(u), 0, heightmapCacheWidth - 1);
			int py = Mathf.Clamp(Mathf.RoundToInt(v), 0, heightmapCacheHeight - 1);
			return heightmapCache[py * heightmapCacheWidth + px];
		}

		public void ResetToDefault()
		{
			seed = 0;
			octaves = 4;
			frequency = 0.01f;
			amplitude = 32f;
			persistence = 0.5f;
			lacunarity = 2f;
			biomeFrequency = 0.005f;
		}
	}
}
