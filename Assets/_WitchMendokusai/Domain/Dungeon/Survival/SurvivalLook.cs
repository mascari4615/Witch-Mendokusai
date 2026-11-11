using System.Collections.Generic;
using UnityEngine;

namespace WitchMendokusai
{
	// 생존 맵 전용 재질 바꾸기. 풀은 빛 받는 재질로, 지면은 색만 밝힌 복제본으로
	// 원본 재질 자산은 그대로라 마을과 다른 스테이지는 영향 없음
	public static class SurvivalLook
	{
		private static readonly int BASE_COLOR_ID = Shader.PropertyToID("_BaseColor");
		private static readonly int COLOR_ID = Shader.PropertyToID("_Color");
		private static readonly Dictionary<Material, Material> groundCopies = new();

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void ResetCache() => groundCopies.Clear();

		public static Material Map(Material original, SurvivalFeelConfig config)
		{
			if (original == null || config == null || config.UseMapLook == false)
				return original;
			if (original == config.GrassSourceMaterial && config.GrassLitMaterial != null)
				return config.GrassLitMaterial;
			if (original == config.GroundSourceMaterial)
				return GroundCopy(original, config.GroundColorScale);
			return original;
		}

		public static void Apply(Renderer renderer, SurvivalFeelConfig config)
		{
			Material[] materials = renderer.sharedMaterials;
			bool changed = false;
			for (int i = 0; i < materials.Length; i++)
			{
				Material mapped = Map(materials[i], config);
				if (mapped != materials[i])
				{
					materials[i] = mapped;
					changed = true;
				}
			}
			if (changed)
				renderer.sharedMaterials = materials;
		}

		public static void ApplyAll(GameObject root, SurvivalFeelConfig config)
		{
			if (root == null || config == null || config.UseMapLook == false)
				return;
			foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
				Apply(renderer, config);
		}

		private static Material GroundCopy(Material original, float scale)
		{
			if (groundCopies.TryGetValue(original, out Material copy) && copy != null)
				return copy;
			copy = new Material(original) { name = original.name + " Survival" };
			if (copy.HasProperty(BASE_COLOR_ID))
				copy.SetColor(BASE_COLOR_ID, Scale(copy.GetColor(BASE_COLOR_ID), scale));
			if (copy.HasProperty(COLOR_ID))
				copy.SetColor(COLOR_ID, Scale(copy.GetColor(COLOR_ID), scale));
			groundCopies[original] = copy;
			return copy;
		}

		private static Color Scale(Color color, float scale) => new(color.r * scale, color.g * scale, color.b * scale, color.a);
	}
}
