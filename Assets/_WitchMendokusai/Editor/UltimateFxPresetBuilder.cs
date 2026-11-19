using UnityEditor;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 궁극기 연출 에셋 (UltimateFx) 세 벌을 만들거나 갱신. 값만 다른 에셋이라 새 연출은 이 표에 한 줄 더
	/// 메뉴: WM/CombatFx/Build Ultimate Presets. 개발 창 명령 fx ultimate [이름 일부] 로 재생
	/// </summary>
	public static class UltimateFxPresetBuilder
	{
		private const string FX_FOLDER = "Assets/_WitchMendokusai/Domain/Dungeon/Fx";

		private struct Preset
		{
			public string AssetName;
			public Color Core;
			public Color Mid;
			public Color Deep;
			public Color Shade;
			public Color Dust;
			public Color Rock;
			public int CrackSeed;
			public float Radius;
			public float Distortion;
			public float ShakeAmplitude;
			public int DebrisCount;
			public int EmberCount;
		}

		[MenuItem("WM/CombatFx/Build Ultimate Presets")]
		public static void Build()
		{
			Preset[] presets =
			{
				new()
				{
					AssetName = "Ultimate_1_Thunder",
					Core = new Color(1.5f, 1.55f, 1.8f), Mid = new Color(1.6f, 1.3f, 0.3f), Deep = new Color(0.75f, 0.45f, 0.06f),
					Shade = new Color(0.02f, 0.02f, 0.07f), Dust = new Color(0.5f, 0.46f, 0.42f, 0.8f), Rock = new Color(0.6f, 0.56f, 0.52f),
					CrackSeed = 7, Radius = 7f, Distortion = 0.06f, ShakeAmplitude = 1.6f, DebrisCount = 52, EmberCount = 70,
				},
				new()
				{
					AssetName = "Ultimate_2_Inferno",
					Core = new Color(1.6f, 1.45f, 1.1f), Mid = new Color(1.5f, 0.7f, 0.15f), Deep = new Color(0.85f, 0.1f, 0.03f),
					Shade = new Color(0.07f, 0.01f, 0.01f), Dust = new Color(0.55f, 0.4f, 0.3f, 0.85f), Rock = new Color(0.62f, 0.46f, 0.38f),
					CrackSeed = 19, Radius = 7.5f, Distortion = 0.07f, ShakeAmplitude = 2f, DebrisCount = 70, EmberCount = 130,
				},
				new()
				{
					AssetName = "Ultimate_3_Frost",
					Core = new Color(1.3f, 1.65f, 1.85f), Mid = new Color(0.35f, 0.9f, 1.6f), Deep = new Color(0.08f, 0.28f, 0.8f),
					Shade = new Color(0.01f, 0.03f, 0.08f), Dust = new Color(0.72f, 0.82f, 0.92f, 0.75f), Rock = new Color(0.75f, 0.85f, 0.95f),
					CrackSeed = 31, Radius = 6f, Distortion = 0.05f, ShakeAmplitude = 1.1f, DebrisCount = 90, EmberCount = 40,
				},
			};

			Shader sprite = Shader.Find("WM/Fx/UltimateSprite");
			Shader shockwave = Shader.Find("WM/Fx/UltimateShockwave");
			CombatFx telegraph = AssetDatabase.LoadAssetAtPath<CombatFx>(FX_FOLDER + "/Presets/Telegraph_3_CircleSoft.asset");
			GameObject grass = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_WitchMendokusai/Domain/World/Parts/Grass/Grass.prefab");
			GameObject shrub = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_WitchMendokusai/Domain/World/Parts/Grass/Shrub.prefab");
			GameObject tree = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_WitchMendokusai/Domain/World/Parts/Tree/Tree.prefab");
			if (sprite == null || shockwave == null)
				throw new System.InvalidOperationException("셰이더 WM/Fx/UltimateSprite, UltimateShockwave 를 찾지 못함 (임포트 전)");

			foreach (Preset preset in presets)
			{
				string path = FX_FOLDER + "/Presets/" + preset.AssetName + ".asset";
				UltimateFx fx = AssetDatabase.LoadAssetAtPath<UltimateFx>(path);
				if (fx == null)
				{
					fx = ScriptableObject.CreateInstance<UltimateFx>();
					AssetDatabase.CreateAsset(fx, path);
				}

				SerializedObject serialized = new(fx);
				serialized.FindProperty("spriteShader").objectReferenceValue = sprite;
				serialized.FindProperty("shockwaveShader").objectReferenceValue = shockwave;
				serialized.FindProperty("telegraph").objectReferenceValue = telegraph;
				SerializedProperty palette = serialized.FindProperty("palette");
				palette.FindPropertyRelative("Core").colorValue = preset.Core;
				palette.FindPropertyRelative("Mid").colorValue = preset.Mid;
				palette.FindPropertyRelative("Deep").colorValue = preset.Deep;
				palette.FindPropertyRelative("Shade").colorValue = preset.Shade;
				palette.FindPropertyRelative("Dust").colorValue = preset.Dust;
				palette.FindPropertyRelative("Rock").colorValue = preset.Rock;
				serialized.FindProperty("crackSeed").intValue = preset.CrackSeed;
				serialized.FindProperty("flashPeak").floatValue = 0.78f;
				serialized.FindProperty("flashHoldSeconds").floatValue = 0.05f;
				serialized.FindProperty("washPeak").floatValue = 0.22f;
				serialized.FindProperty("impactRadius").floatValue = preset.Radius;
				serialized.FindProperty("distortion").floatValue = preset.Distortion;
				serialized.FindProperty("shakeAmplitude").floatValue = preset.ShakeAmplitude;
				serialized.FindProperty("debrisCount").intValue = preset.DebrisCount;
				serialized.FindProperty("emberCount").intValue = preset.EmberCount;

				SerializedProperty foliage = serialized.FindProperty("foliage");
				foliage.arraySize = 3;
				SetFoliage(foliage.GetArrayElementAtIndex(0), grass, 150, 1f, new Vector2(1f, 1.7f), 1.6f);
				SetFoliage(foliage.GetArrayElementAtIndex(1), shrub, 14, 0.7f, new Vector2(0.8f, 1.3f), 3.5f);
				SetFoliage(foliage.GetArrayElementAtIndex(2), tree, 5, 0.22f, new Vector2(0.45f, 0.7f), 6.5f);
				serialized.ApplyModifiedPropertiesWithoutUndo();
				EditorUtility.SetDirty(fx);
			}
			AssetDatabase.SaveAssets();
			Debug.Log($"[UltimateFx] 프리셋 {presets.Length}개 갱신");
		}

		private static void SetFoliage(SerializedProperty element, GameObject prefab, int count, float flexibility, Vector2 scaleRange, float minDistance)
		{
			element.FindPropertyRelative("Prefab").objectReferenceValue = prefab;
			element.FindPropertyRelative("Count").intValue = count;
			element.FindPropertyRelative("Flexibility").floatValue = flexibility;
			element.FindPropertyRelative("ScaleRange").vector2Value = scaleRange;
			element.FindPropertyRelative("MinDistance").floatValue = minDistance;
		}
	}
}
