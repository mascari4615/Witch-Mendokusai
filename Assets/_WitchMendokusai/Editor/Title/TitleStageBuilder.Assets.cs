using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai
{
	// 시작 화면 무대 전용 자산 (재질, 하늘 설정, 글자판) 만들기
	public static partial class TitleStageBuilder
	{
		private static Material LoadOrCreateGroundMaterial(TitleStageRecipe recipe)
		{
			Material material = AssetDatabase.LoadAssetAtPath<Material>(GROUND_MATERIAL_PATH);
			if (material == null)
			{
				material = new Material(recipe.GroundSource);
				AssetDatabase.CreateAsset(material, GROUND_MATERIAL_PATH);
			}
			else
			{
				material.CopyPropertiesFromMaterial(recipe.GroundSource);
			}
			material.SetColor("_BaseColor", recipe.GroundColor);
			EditorUtility.SetDirty(material);
			return material;
		}

		// 무대 전용 재질. 원본 공용 재질은 건드리지 않음
		private static Dictionary<Material, Material> BuildMaterialSwaps(TitleStageRecipe recipe)
		{
			Material grass = LoadOrCreateMaterial(GRASS_MATERIAL_PATH, recipe.GrassLit.shader);
			grass.CopyPropertiesFromMaterial(recipe.GrassLit);
			grass.SetColor("_BaseColor", recipe.GrassColor);
			EditorUtility.SetDirty(grass);

			// 원본 잎 셰이더는 색 입력이 없어 Simple Lit 으로 같은 무늬를 씀
			Material leaf = LoadOrCreateMaterial(LEAF_MATERIAL_PATH, Shader.Find("Universal Render Pipeline/Simple Lit"));
			leaf.SetTexture("_BaseMap", recipe.LeafSource.GetTexture("_Texture2D"));
			leaf.SetColor("_BaseColor", recipe.LeafColor);
			leaf.SetFloat("_AlphaClip", 1f);
			leaf.SetFloat("_Cutoff", recipe.LeafCutoff);
			leaf.EnableKeyword("_ALPHATEST_ON");
			leaf.SetFloat("_Cull", 0f);
			leaf.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
			EditorUtility.SetDirty(leaf);

			return new Dictionary<Material, Material>
			{
				{ recipe.GrassSource, grass },
				{ recipe.GrassLit, grass },
				{ recipe.LeafSource, leaf },
			};
		}

		private static Material[] LoadOrCreateFlowerMaterials(TitleStageRecipe recipe)
		{
			Material[] materials = new Material[recipe.FlowerColors.Length];
			for (int i = 0; i < materials.Length; i++)
			{
				materials[i] = LoadOrCreateMaterial(string.Format(FLOWER_MATERIAL_PATH, i), Shader.Find("Universal Render Pipeline/Unlit"));
				materials[i].SetColor("_BaseColor", recipe.FlowerColors[i]);
				EditorUtility.SetDirty(materials[i]);
			}
			return materials;
		}

		private static Material LoadOrCreateMaterial(string path, Shader shader)
		{
			Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
			if (material != null)
			{
				material.shader = shader;
				return material;
			}
			material = new Material(shader);
			AssetDatabase.CreateAsset(material, path);
			return material;
		}

		// 공용 하늘 설정을 복사해 시작 화면 색만 덮음
		private static SkyPresetSO LoadOrCreateSkyPreset(TitleStageRecipe recipe)
		{
			SkyPresetSO preset = AssetDatabase.LoadAssetAtPath<SkyPresetSO>(SKY_PRESET_PATH);
			if (preset == null)
			{
				preset = ScriptableObject.CreateInstance<SkyPresetSO>();
				AssetDatabase.CreateAsset(preset, SKY_PRESET_PATH);
			}
			EditorUtility.CopySerialized(recipe.SkyPreset, preset);
			preset.name = System.IO.Path.GetFileNameWithoutExtension(SKY_PRESET_PATH);
			SerializedObject serialized = new(preset);
			serialized.FindProperty("<ZenithColor>k__BackingField").gradientValue = Flat(recipe.SkyZenith);
			serialized.FindProperty("<HorizonColor>k__BackingField").gradientValue = Flat(recipe.SkyHorizon);
			serialized.FindProperty("<FogColor>k__BackingField").gradientValue = Flat(recipe.SkyHorizon);
			serialized.FindProperty("<CloudColor>k__BackingField").gradientValue = Flat(recipe.CloudColor);
			serialized.FindProperty("<CloudCoverage>k__BackingField").animationCurveValue = AnimationCurve.Constant(0f, 1f, recipe.CloudCoverage);
			serialized.FindProperty("<CloudScale>k__BackingField").floatValue = recipe.CloudScale;
			serialized.FindProperty("<CloudSoftness>k__BackingField").floatValue = recipe.CloudSoftness;
			serialized.FindProperty("<CloudHeight>k__BackingField").floatValue = recipe.CloudHeight;
			serialized.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(preset);
			return preset;
		}

		private static Gradient Flat(Color color)
		{
			Gradient gradient = new();
			gradient.SetKeys(new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
			return gradient;
		}

		// 동적 글자판. 실행 중 필요한 글자만 채움. 흐린 그림자는 기본 재질의 밑그림
		private static TMP_FontAsset LoadOrCreateFont(Font source, string path, TitleStageRecipe recipe)
		{
			TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
			if (font == null)
			{
				font = TMP_FontAsset.CreateFontAsset(source);
				font.name = System.IO.Path.GetFileNameWithoutExtension(path);
				AssetDatabase.CreateAsset(font, path);
				font.material.name = font.name + " Material";
				AssetDatabase.AddObjectToAsset(font.material, font);
				font.atlasTexture.name = font.name + " Atlas";
				AssetDatabase.AddObjectToAsset(font.atlasTexture, font);
			}
			Material material = font.material;
			material.EnableKeyword("UNDERLAY_ON");
			material.SetColor("_UnderlayColor", recipe.TextShadowColor);
			material.SetFloat("_UnderlayOffsetY", recipe.TextShadowOffsetY);
			material.SetFloat("_UnderlaySoftness", recipe.TextShadowSoftness);
			EditorUtility.SetDirty(material);
			EditorUtility.SetDirty(font);
			return font;
		}
	}
}
