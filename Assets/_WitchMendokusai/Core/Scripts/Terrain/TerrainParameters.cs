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

		[Header("Biome")]
		[SerializeField] private float biomeFrequency = 0.005f;
		[SerializeField] private List<BiomeWeight> biomes = new();

		public int Seed => seed;
		public int Octaves => octaves;
		public float Frequency => frequency;
		public float Amplitude => amplitude;
		public float Persistence => persistence;
		public float Lacunarity => lacunarity;
		public float BiomeFrequency => biomeFrequency;
		public IReadOnlyList<BiomeWeight> Biomes => biomes;

		public void SetSeed(int value) => seed = value;
		public void SetOctaves(int value) => octaves = Mathf.Clamp(value, 1, 8);
		public void SetFrequency(float value) => frequency = Mathf.Max(0.0001f, value);
		public void SetAmplitude(float value) => amplitude = value;
		public void SetPersistence(float value) => persistence = Mathf.Clamp01(value);
		public void SetLacunarity(float value) => lacunarity = Mathf.Max(1f, value);
		public void SetBiomeFrequency(float value) => biomeFrequency = Mathf.Max(0.0001f, value);

		public void SetBiomes(List<BiomeWeight> value) => biomes = value ?? new();

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
