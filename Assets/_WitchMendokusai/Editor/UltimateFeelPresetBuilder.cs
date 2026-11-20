using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace WitchMendokusai
{
	/// <summary>
	/// 강한 판 V2 넷 (화면 연출을 입힌 궁극기) 프리셋 생성
	/// - body: V1 강한 판 에셋을 복사한 별개 에셋 (V1 은 손대지 않음). 흔들림과 밝은 섬광은 구동기 몫이라 body 쪽 값만 줄임
	/// - feel: 화면 연출 박자표. 처음 값은 UltimateFeelV2Seeds
	/// - 렌더러에 임팩트 화면 왜곡 기능 (UltimateImpactScreenFeature) 이 없으면 붙임
	/// - 이미 있는 에셋 값은 그대로 (인스펙터 정본). 처음 값을 다시 넣는 것은 Reseed 메뉴만
	/// 메뉴: WM/CombatFx/Build Ultimate Feel V2, WM/CombatFx/Reseed Ultimate Feel V2
	/// </summary>
	public static class UltimateFeelPresetBuilder
	{
		private const string PRESET_FOLDER = "Assets/_WitchMendokusai/Domain/Dungeon/Fx/Presets";
		private const string FEEL_FOLDER = PRESET_FOLDER + "/FeelV2";
		private const string COMFORT_PATH = FEEL_FOLDER + "/UltFeel_Comfort.asset";

		private sealed class Entry
		{
			public string Name;
			public string DevKey;
			public string SourceBody;
			public float TargetForward;
			public Func<UltimateScreenFeel.Swell[]> Swells;
			public Func<UltimateScreenFeel.Beat[]> Beats;
			public UltimateScreenFeel.TraumaSettings Trauma;
			public Action<SerializedObject> TuneBody;
		}

		private static Entry[] Entries() => new[]
		{
			new Entry
			{
				Name = "SkySwordHeavyV2", DevKey = "ult3d_heavy_v2", SourceBody = "Ult3D_02_SkySwordHeavy", TargetForward = 2.2f,
				Swells = UltimateFeelV2Seeds.SkySwordSwells, Beats = UltimateFeelV2Seeds.SkySwordBeats,
				Trauma = UltimateFeelV2Seeds.Trauma(0.022f, 15f, 2f, 0.75f),
				TuneBody = body =>
				{
					// 접지 한 방의 세로 덩어리, 점유, 핵 밝기를 레퍼런스 위쪽으로
					SetFloat(body, "beamHeight", 22f);
					SetFloat(body, "beamFullRadius", 1f);
					SetFloat(body, "beamImpactBoost", 1.5f);
					SetFloat(body, "beamThickSeconds", 0.28f);
					SetInt(body, "crownSpikeCount", 80);
					SetFloat(body, "goldRingRadius", 7f);
					SetFloat(body, "shakeStrength", 0f);
					SetFloat(body, "exposureFlash", 0.6f);
					SetFloat(body, "chromaticImpact", 0.1f);
				},
			},
			new Entry
			{
				Name = "SpearCrownHeavyV2", DevKey = "crown_heavy_v2", SourceBody = "Ult3D_SpearCrownHeavy", TargetForward = 2.8f,
				Swells = UltimateFeelV2Seeds.CrownSwells, Beats = UltimateFeelV2Seeds.CrownBeats,
				Trauma = UltimateFeelV2Seeds.Trauma(0.02f, 16f, 2.4f, 0.65f),
				TuneBody = body =>
				{
					SetFloat(body, "shakeStrength", 0f);
					SetFloat(body, "silhouetteExposure", 0f);
				},
			},
			new Entry
			{
				Name = "CardHeavyV2", DevKey = "card_heavy_v2", SourceBody = "Ult3D_CardHeavy", TargetForward = 2.8f,
				Swells = UltimateFeelV2Seeds.CardSwells, Beats = UltimateFeelV2Seeds.CardBeats,
				Trauma = UltimateFeelV2Seeds.Trauma(0.02f, 14f, 2.2f, 0.7f),
				TuneBody = body => SetFloat(body, "shakeStrength", 0f),
			},
			new Entry
			{
				Name = "MoonHeavyV2", DevKey = "moon_heavy_v2", SourceBody = "Ult3D_MoonHeavy", TargetForward = 2.8f,
				Swells = UltimateFeelV2Seeds.MoonSwells, Beats = UltimateFeelV2Seeds.MoonBeats,
				Trauma = UltimateFeelV2Seeds.Trauma(0.02f, 10f, 1.4f, 0.8f),
				TuneBody = body =>
				{
					SetFloat(body, "shakeStrength", 0f);
					SetFloat(body, "flashExposure", 0.4f);
					SetFloat(body, "secondFlashExposure", 0.3f);
				},
			},
		};

		[MenuItem("WM/CombatFx/Build Ultimate Feel V2")]
		public static void Build() => Run(false);

		[MenuItem("WM/CombatFx/Reseed Ultimate Feel V2")]
		public static void Reseed() => Run(true);

		private static void Run(bool reseed)
		{
			if (AssetDatabase.IsValidFolder(FEEL_FOLDER) == false)
				AssetDatabase.CreateFolder(PRESET_FOLDER, "FeelV2");
			InstallRendererFeature();
			UltimateFeelComfort comfort = LoadOrCreate<UltimateFeelComfort>(COMFORT_PATH, out _);
			foreach (Entry entry in Entries())
				BuildEntry(entry, comfort, reseed);
			AssetDatabase.SaveAssets();
			Debug.Log($"[UltimateFx] 강한 판 V2 {Entries().Length}개 {(reseed ? "처음 값 다시 넣음" : "갱신")}");
		}

		private static void BuildEntry(Entry entry, UltimateFeelComfort comfort, bool reseed)
		{
			string sourcePath = PRESET_FOLDER + "/" + entry.SourceBody + ".asset";
			string bodyPath = FEEL_FOLDER + "/Ult3D_" + entry.Name + "_Body.asset";
			UltimateFx source = AssetDatabase.LoadAssetAtPath<UltimateFx>(sourcePath);
			if (source == null)
				throw new InvalidOperationException($"V1 강한 판 없음: {sourcePath}");
			bool bodyCreated = AssetDatabase.LoadAssetAtPath<UltimateFx>(bodyPath) == null;
			if (bodyCreated && AssetDatabase.CopyAsset(sourcePath, bodyPath) == false)
				throw new InvalidOperationException($"복사 실패: {sourcePath} -> {bodyPath}");
			UltimateFx body = AssetDatabase.LoadAssetAtPath<UltimateFx>(bodyPath);
			if (bodyCreated || reseed)
			{
				SerializedObject bodySerialized = new(body);
				SetString(bodySerialized, "devKey", string.Empty);
				entry.TuneBody(bodySerialized);
				bodySerialized.ApplyModifiedPropertiesWithoutUndo();
				EditorUtility.SetDirty(body);
			}

			string feelPath = FEEL_FOLDER + "/UltFeel_" + entry.Name + ".asset";
			UltimateScreenFeel feel = LoadOrCreate<UltimateScreenFeel>(feelPath, out bool feelCreated);
			if (feelCreated || reseed)
			{
				feel.EditorSeed(comfort, entry.Trauma, entry.Swells(), entry.Beats(), entry.TargetForward);
				EditorUtility.SetDirty(feel);
			}

			string wrapperPath = PRESET_FOLDER + "/Ult3D_" + entry.Name + ".asset";
			FeelUltimateFx wrapper = LoadOrCreate<FeelUltimateFx>(wrapperPath, out bool wrapperCreated);
			SerializedObject wrapperSerialized = new(wrapper);
			SerializedObject sourceSerialized = new(body);
			// 진행기가 읽는 공통 값 (셰이더, 섬광 상한, 임시 풀, 꼬리) 은 처음 만들 때와 다시 넣기 때만 body 에서 복사. 그 뒤 정본은 인스펙터
			// body 가 FeelUltimateFx 기본 에셋 (공통 프리셋 생성기가 만든 빈 것) 이었으면 처음 만든 것과 같게 봄
			bool copyCommon = wrapperCreated || reseed || wrapper.Body == null;
			foreach (string field in copyCommon ? new[] { "spriteShader", "shockwaveShader", "gradeShader", "overlayFlashCap", "backgroundFlashCap", "spawnTemporaryFoliage", "foliageAreaRadius", "foliage", "foliageResponse", "tailSeconds", "screenSortingOrder" } : Array.Empty<string>())
				wrapperSerialized.CopyFromSerializedProperty(Property(sourceSerialized, field));
			SetString(wrapperSerialized, "devKey", entry.DevKey);
			Property(wrapperSerialized, "body").objectReferenceValue = body;
			Property(wrapperSerialized, "feel").objectReferenceValue = feel;
			wrapperSerialized.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(wrapper);
		}

		private static T LoadOrCreate<T>(string path, out bool created) where T : ScriptableObject
		{
			T asset = AssetDatabase.LoadAssetAtPath<T>(path);
			created = asset == null;
			if (created)
			{
				asset = ScriptableObject.CreateInstance<T>();
				AssetDatabase.CreateAsset(asset, path);
			}
			return asset;
		}

		/// <summary> 지금 파이프라인의 기본 렌더러에 임팩트 화면 왜곡 기능을 한 번 붙임 </summary>
		private static void InstallRendererFeature()
		{
			UniversalRenderPipelineAsset pipeline = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
			if (pipeline == null)
				throw new InvalidOperationException("URP 파이프라인 에셋이 아님");
			SerializedObject pipelineSerialized = new(pipeline);
			ScriptableRendererData rendererData = (ScriptableRendererData)Property(pipelineSerialized, "m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue;
			foreach (ScriptableRendererFeature existing in rendererData.rendererFeatures)
			{
				if (existing is UltimateImpactScreenFeature)
					return;
			}
			UltimateImpactScreenFeature feature = ScriptableObject.CreateInstance<UltimateImpactScreenFeature>();
			feature.name = "UltimateImpactScreen";
			AssetDatabase.AddObjectToAsset(feature, rendererData);
			SerializedObject featureSerialized = new(feature);
			Property(featureSerialized, "shader").objectReferenceValue = Shader.Find("WM/Fx/UltimateImpactScreen");
			featureSerialized.ApplyModifiedPropertiesWithoutUndo();
			AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId);
			SerializedObject dataSerialized = new(rendererData);
			SerializedProperty features = Property(dataSerialized, "m_RendererFeatures");
			SerializedProperty map = Property(dataSerialized, "m_RendererFeatureMap");
			features.arraySize++;
			features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
			map.arraySize++;
			map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
			dataSerialized.ApplyModifiedPropertiesWithoutUndo();
			rendererData.SetDirty();
			EditorUtility.SetDirty(rendererData);
			Debug.Log($"[UltimateFx] 렌더러 {rendererData.name} 에 UltimateImpactScreen 기능 추가");
		}

		private static SerializedProperty Property(SerializedObject target, string name)
		{
			SerializedProperty property = target.FindProperty(name);
			if (property == null)
				throw new InvalidOperationException($"{target.targetObject.name}: 칸 {name} 없음");
			return property;
		}

		private static void SetFloat(SerializedObject target, string name, float value) => Property(target, name).floatValue = value;

		private static void SetInt(SerializedObject target, string name, int value) => Property(target, name).intValue = value;

		private static void SetString(SerializedObject target, string name, string value) => Property(target, name).stringValue = value;
	}
}
