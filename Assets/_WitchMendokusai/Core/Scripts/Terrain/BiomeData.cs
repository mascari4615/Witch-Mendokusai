using System;
using UnityEngine;

namespace WitchMendokusai
{
	[CreateAssetMenu(fileName = nameof(BiomeData), menuName = "WM/Terrain/" + nameof(BiomeData))]
	public class BiomeData : ScriptableObject
	{
		[SerializeField] private string biomeName = "Biome";
		[SerializeField] private Color previewColor = Color.green;

		public string BiomeName => biomeName;
		public Color PreviewColor => previewColor;

		public void SetBiomeName(string value) => biomeName = value;
		public void SetPreviewColor(Color value) => previewColor = value;
	}

	[Serializable]
	public class BiomeWeight
	{
		[SerializeField] private BiomeData biome;
		[SerializeField, Min(0f)] private float weight = 1f;

		public BiomeData Biome => biome;
		public float Weight => weight;

		public BiomeWeight() { }
		public BiomeWeight(BiomeData biome, float weight)
		{
			this.biome = biome;
			this.weight = weight;
		}
	}
}
