using System;
using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	[CreateAssetMenu(fileName = nameof(BiomeData), menuName = "WM/Terrain/" + nameof(BiomeData))]
	public class BiomeData : ScriptableObject
	{
		[SerializeField] private string biomeName = "Biome";
		[SerializeField] private Color previewColor = Color.green;

		[Header("Voxel Spawn")]
		[SerializeField] private BlockData surfaceBlock;
		[SerializeField] private BlockData subsurfaceBlock;
		[SerializeField, Min(1)] private int subsurfaceDepth = 3;

		[Header("Entity Spawn")]
		[SerializeField] private List<BiomeEntitySpawnRule> entitySpawns = new();

		public string BiomeName => biomeName;
		public Color PreviewColor => previewColor;
		public BlockData SurfaceBlock => surfaceBlock;
		public BlockData SubsurfaceBlock => subsurfaceBlock;
		public int SubsurfaceDepth => subsurfaceDepth;
		public IReadOnlyList<BiomeEntitySpawnRule> EntitySpawns => entitySpawns;

		public void SetBiomeName(string value) => biomeName = value;
		public void SetPreviewColor(Color value) => previewColor = value;
		public void SetSurfaceBlock(BlockData value) => surfaceBlock = value;
		public void SetSubsurfaceBlock(BlockData value) => subsurfaceBlock = value;
		public void SetSubsurfaceDepth(int value) => subsurfaceDepth = Mathf.Max(1, value);
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

	/// <summary>
	/// 청크 cell 당 entity (나무/꽃/바위 등) 자연 spawn 확률.
	/// ChunkEntitySpawner가 청크별 결정적 RNG로 평가해 인스턴스화.
	/// </summary>
	[Serializable]
	public class BiomeEntitySpawnRule
	{
		[SerializeField] private EntityData entity;
		[SerializeField, Range(0f, 1f)] private float density = 0.05f;

		public EntityData Entity => entity;
		public float Density => density;

		public BiomeEntitySpawnRule() { }
		public BiomeEntitySpawnRule(EntityData entity, float density)
		{
			this.entity = entity;
			this.density = density;
		}
	}
}
