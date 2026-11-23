using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 종류마다 씨앗 몇 개로 미리 만든 메시 묶음. 같은 모양은 메시와 재질을 공유
	public sealed class BreakableLibrary
	{
		private static readonly int BASE_MAP_ID = Shader.PropertyToID("_BaseMap");
		private static readonly int MAIN_TEX_ID = Shader.PropertyToID("_MainTex");
		private static readonly int BASE_COLOR_ID = Shader.PropertyToID("_BaseColor");

		public sealed class Variant
		{
			public Mesh Mesh;
			public Mesh[] ShardMeshes;
			// 조각마다 누웠을 때 중심 높이와 날 때 띄울 거리 (크기 1 기준, 메시 경계에서)
			public float[] ShardRest;
			public float[] ShardFly;
		}

		private readonly List<Object> owned = new();

		public Material Material { get; private set; }
		public Variant[][] Variants { get; private set; }

		public static BreakableLibrary Build(BreakableSettings settings)
		{
			if (settings.BaseMaterial == null)
				throw new System.InvalidOperationException($"{nameof(BreakableSettings)}.{nameof(BreakableSettings.BaseMaterial)} 미연결");
			if (settings.Kinds.Length == 0)
				throw new System.InvalidOperationException($"{nameof(BreakableSettings)}.{nameof(BreakableSettings.Kinds)} 비어 있음");

			int capacity = 0;
			foreach (BreakableKind kind in settings.Kinds)
			{
				if (kind.Palette.Length == 0)
					throw new System.InvalidOperationException($"{nameof(BreakableKind)} {kind.Name} 팔레트 비어 있음");
				capacity += kind.Palette.Length * settings.VariantsPerKind;
			}

			BreakableLibrary library = new();
			BreakableAtlas atlas = new(capacity, settings.StripPixels);
			library.Variants = new Variant[settings.Kinds.Length][];
			for (int kindIndex = 0; kindIndex < settings.Kinds.Length; kindIndex++)
			{
				BreakableKind kind = settings.Kinds[kindIndex];
				library.Variants[kindIndex] = new Variant[settings.VariantsPerKind];
				for (int variantIndex = 0; variantIndex < settings.VariantsPerKind; variantIndex++)
				{
					System.Random random = new(unchecked(settings.Seed ^ (kindIndex + 1) * 92821 ^ (variantIndex + 1) * 68917));
					int[] rows = new int[kind.Palette.Length];
					for (int slot = 0; slot < rows.Length; slot++)
						rows[slot] = atlas.Add(Jitter(kind.Palette[slot], settings.ColorJitter, random));

					BreakableMeshBuilder builder = new(atlas, random, settings.GrainPerMeter);
					Vector3 size = Vector3.Scale(kind.Size, new Vector3(
						1f + builder.Range(-kind.SizeJitter, kind.SizeJitter),
						1f + builder.Range(-kind.SizeJitter, kind.SizeJitter),
						1f + builder.Range(-kind.SizeJitter, kind.SizeJitter)));
					builder.SetMatrix(Matrix4x4.Rotate(Quaternion.Euler(
						builder.Range(-settings.TiltDegrees, settings.TiltDegrees), 0f,
						builder.Range(-settings.TiltDegrees, settings.TiltDegrees))));
					BreakableShapes.Build(builder, kind.Shape, size, rows);
					Variant variant = new()
					{
						Mesh = library.Own(builder.Build($"Breakable_{kind.Name}_{variantIndex}")),
						ShardMeshes = new Mesh[BreakableShardShapes.SHARDS_PER_VARIANT],
						ShardRest = new float[BreakableShardShapes.SHARDS_PER_VARIANT],
						ShardFly = new float[BreakableShardShapes.SHARDS_PER_VARIANT],
					};
					for (int shard = 0; shard < variant.ShardMeshes.Length; shard++)
					{
						BreakableShardShapes.Build(builder, kind.Shape, rows, shard);
						Mesh shardMesh = library.Own(builder.Build($"BreakableShard_{kind.Name}_{variantIndex}_{shard}"));
						Bounds bounds = shardMesh.bounds;
						variant.ShardMeshes[shard] = shardMesh;
						variant.ShardRest[shard] = bounds.extents.y - bounds.center.y;
						variant.ShardFly[shard] = bounds.extents.magnitude + bounds.center.magnitude;
					}
					library.Variants[kindIndex][variantIndex] = variant;
				}
			}

			Texture2D texture = library.Own(atlas.Build(settings.GrainPixels, settings.GrainStrength, settings.Seed));
			Material material = library.Own(new Material(settings.BaseMaterial) { name = "Breakable", enableInstancing = true, hideFlags = HideFlags.DontSave });
			material.SetTexture(BASE_MAP_ID, texture);
			material.SetTextureScale(BASE_MAP_ID, Vector2.one);
			if (material.HasProperty(MAIN_TEX_ID))
			{
				material.SetTexture(MAIN_TEX_ID, texture);
				material.SetTextureScale(MAIN_TEX_ID, Vector2.one);
			}
			material.SetColor(BASE_COLOR_ID, Color.white);
			library.Material = material;
			return library;
		}

		private T Own<T>(T asset) where T : Object
		{
			asset.hideFlags = HideFlags.DontSave;
			owned.Add(asset);
			return asset;
		}

		private static Color Jitter(Color color, float amount, System.Random random)
		{
			Color.RGBToHSV(color, out float hue, out float saturation, out float value);
			hue = Mathf.Repeat(hue + ((float)random.NextDouble() * 2f - 1f) * amount * .3f, 1f);
			saturation = Mathf.Clamp01(saturation + ((float)random.NextDouble() * 2f - 1f) * amount);
			value = Mathf.Clamp01(value + ((float)random.NextDouble() * 2f - 1f) * amount);
			return Color.HSVToRGB(hue, saturation, value);
		}

		public void Destroy()
		{
			foreach (Object asset in owned)
				if (asset != null)
					Object.Destroy(asset);
			owned.Clear();
		}
	}
}
