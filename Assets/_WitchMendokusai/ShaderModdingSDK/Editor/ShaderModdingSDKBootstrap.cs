using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace WitchMendokusai
{
	// 샘플 셰이더팩 자동 생성. Domain Reload 시 missing 검사 + 누락이면 자동. (TASK-WM-055 P1-F + WM-058 P2-D)
	// 모더가 자기 셰이더팩 만들 때 이 폴더를 복사해서 시작점으로 사용.
	//   - cozy-night    : PostProcess 슬롯 (P1)
	//   - aurora-sky    : Skybox 슬롯 (P2-D) — uniform contract 시연
	//   - cartoon-water : Water 슬롯 (P2-D) — MeshRenderer.sharedMaterial 교체 시연
	public static class ShaderModdingSDKBootstrap
	{
		private const string SAMPLE_FOLDER = "Assets/_WitchMendokusai/ShaderModdingSDK/Samples/cozy-night";
		private const string PROFILE_ASSET_PATH = SAMPLE_FOLDER + "/CozyNightVolumeProfile.asset";
		private const string MANIFEST_PATH = SAMPLE_FOLDER + "/manifest.json";

		private const string AURORA_FOLDER = "Assets/_WitchMendokusai/ShaderModdingSDK/Samples/aurora-sky";
		private const string AURORA_SHADER_PATH = AURORA_FOLDER + "/AuroraSky.shader";
		private const string AURORA_MATERIAL_PATH = AURORA_FOLDER + "/AuroraSkyMaterial.mat";
		private const string AURORA_MANIFEST_PATH = AURORA_FOLDER + "/manifest.json";

		private const string WATER_FOLDER = "Assets/_WitchMendokusai/ShaderModdingSDK/Samples/cartoon-water";
		private const string WATER_SHADER_PATH = WATER_FOLDER + "/CartoonWater.shader";
		private const string WATER_MATERIAL_PATH = WATER_FOLDER + "/CartoonWaterMaterial.mat";
		private const string WATER_MANIFEST_PATH = WATER_FOLDER + "/manifest.json";

		[InitializeOnLoadMethod]
		private static void AutoBootstrapIfMissing()
		{
			if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(PROFILE_ASSET_PATH) == null || File.Exists(MANIFEST_PATH) == false)
				CreateSample();

			if (AssetDatabase.LoadAssetAtPath<Material>(AURORA_MATERIAL_PATH) == null || File.Exists(AURORA_MANIFEST_PATH) == false)
				CreateAuroraSkySample();

			if (AssetDatabase.LoadAssetAtPath<Material>(WATER_MATERIAL_PATH) == null || File.Exists(WATER_MANIFEST_PATH) == false)
				CreateCartoonWaterSample();
		}

		[MenuItem("WM/Setup/Recreate cozy-night Sample")]
		private static void RecreateMenuItem()
		{
			if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(PROFILE_ASSET_PATH) != null)
				AssetDatabase.DeleteAsset(PROFILE_ASSET_PATH);
			if (File.Exists(MANIFEST_PATH))
				File.Delete(MANIFEST_PATH);
			CreateSample();
		}

		[MenuItem("WM/Setup/Recreate aurora-sky Sample")]
		private static void RecreateAuroraSkyMenuItem()
		{
			if (AssetDatabase.LoadAssetAtPath<Material>(AURORA_MATERIAL_PATH) != null)
				AssetDatabase.DeleteAsset(AURORA_MATERIAL_PATH);
			if (File.Exists(AURORA_MANIFEST_PATH))
				File.Delete(AURORA_MANIFEST_PATH);
			CreateAuroraSkySample();
		}

		[MenuItem("WM/Setup/Recreate cartoon-water Sample")]
		private static void RecreateCartoonWaterMenuItem()
		{
			if (AssetDatabase.LoadAssetAtPath<Material>(WATER_MATERIAL_PATH) != null)
				AssetDatabase.DeleteAsset(WATER_MATERIAL_PATH);
			if (File.Exists(WATER_MANIFEST_PATH))
				File.Delete(WATER_MANIFEST_PATH);
			CreateCartoonWaterSample();
		}

		private static void CreateSample()
		{
			if (Directory.Exists(SAMPLE_FOLDER) == false)
				Directory.CreateDirectory(SAMPLE_FOLDER);

			VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PROFILE_ASSET_PATH);
			if (profile == null)
			{
				profile = ScriptableObject.CreateInstance<VolumeProfile>();
				AssetDatabase.CreateAsset(profile, PROFILE_ASSET_PATH);

				// 부드러운 따뜻한 톤 — 플레이어 적용 시 시각 충격 X (모더는 자기 셰이더팩에서 자유 강화).
				// 모더 onboarding 강한 톤 시연 = design doc 「P2-D 모더 가이드」 예시 코드 참고.
				// VolumeComponent 들은 sub-asset 으로 추가 (URP Editor 패턴 — profile.Add<T> 만으론 .asset 저장 안 됨).
				ColorAdjustments colorAdjust = ScriptableObject.CreateInstance<ColorAdjustments>();
				colorAdjust.hideFlags = HideFlags.HideInHierarchy;
				colorAdjust.postExposure.overrideState = true;
				colorAdjust.postExposure.value = 0.2f;
				colorAdjust.colorFilter.overrideState = true;
				colorAdjust.colorFilter.value = new Color(1.0f, 0.85f, 0.75f, 1.0f);
				colorAdjust.saturation.overrideState = true;
				colorAdjust.saturation.value = 10f;
				AssetDatabase.AddObjectToAsset(colorAdjust, profile);
				profile.components.Add(colorAdjust);

				Vignette vignette = ScriptableObject.CreateInstance<Vignette>();
				vignette.hideFlags = HideFlags.HideInHierarchy;
				vignette.intensity.overrideState = true;
				vignette.intensity.value = 0.25f;
				vignette.color.overrideState = true;
				vignette.color.value = new Color(0.15f, 0.05f, 0.20f, 1.0f);
				AssetDatabase.AddObjectToAsset(vignette, profile);
				profile.components.Add(vignette);

				EditorUtility.SetDirty(profile);
				Debug.Log($"[ShaderModdingSDK] Created sample VolumeProfile {PROFILE_ASSET_PATH}");
			}

			if (File.Exists(MANIFEST_PATH) == false)
			{
				string manifestJson = @"{
  ""schemaVersion"": 1,
  ""name"": ""Cozy Night"",
  ""author"": ""Mascari4615"",
  ""version"": ""0.1.0"",
  ""description"": ""Warm color grade — sample shaderpack"",
  ""bundleFile"": ""cozy-night.shaderbundle"",
  ""slots"": [
    {
      ""id"": ""postprocess"",
      ""assetName"": ""CozyNightVolumeProfile"",
      ""blendMode"": ""overlay"",
      ""priority"": 9999
    }
  ]
}";
				File.WriteAllText(MANIFEST_PATH, manifestJson);
				AssetDatabase.ImportAsset(MANIFEST_PATH);
				Debug.Log($"[ShaderModdingSDK] Created sample manifest {MANIFEST_PATH}");
			}

			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
		}

		private static void CreateAuroraSkySample()
		{
			if (Directory.Exists(AURORA_FOLDER) == false)
				Directory.CreateDirectory(AURORA_FOLDER);

			// manifest 먼저 작성 — shader import 진행 상태 무관. Builder 가 manifest 의존하니
			// Material 생성 실패와 별개로 작성되어야 한다 (이중 Reload race 방지).
			if (File.Exists(AURORA_MANIFEST_PATH) == false)
			{
				string auroraManifest = @"{
  ""schemaVersion"": 1,
  ""name"": ""Aurora Sky"",
  ""author"": ""Mascari4615"",
  ""version"": ""0.1.0"",
  ""description"": ""Skybox sample — _WMSkyZenith/Horizon base + green aurora ribbon (밤만)"",
  ""bundleFile"": ""aurora-sky.shaderbundle"",
  ""slots"": [
    {
      ""id"": ""skybox"",
      ""assetName"": ""AuroraSkyMaterial"",
      ""blendMode"": ""uniform"",
      ""priority"": 0
    }
  ]
}";
				File.WriteAllText(AURORA_MANIFEST_PATH, auroraManifest);
				AssetDatabase.ImportAsset(AURORA_MANIFEST_PATH);
				Debug.Log($"[ShaderModdingSDK] Created sample manifest {AURORA_MANIFEST_PATH}");
			}

			// Material 생성 — shader import 끝나야 가능. 안 되면 다음 Domain Reload 에서 재시도.
			Material auroraMaterial = AssetDatabase.LoadAssetAtPath<Material>(AURORA_MATERIAL_PATH);
			if (auroraMaterial == null)
			{
				Shader auroraShader = AssetDatabase.LoadAssetAtPath<Shader>(AURORA_SHADER_PATH);
				if (auroraShader == null)
				{
					AssetDatabase.SaveAssets();
					return;
				}

				auroraMaterial = new Material(auroraShader);
				auroraMaterial.SetColor("_AuroraColor", new Color(0.30f, 1.00f, 0.60f, 1.00f));
				auroraMaterial.SetFloat("_AuroraIntensity", 2.0f);
				auroraMaterial.SetFloat("_AuroraHeight", 0.55f);
				auroraMaterial.SetFloat("_AuroraThickness", 0.18f);
				auroraMaterial.SetFloat("_AuroraWaveAmount", 0.08f);
				auroraMaterial.SetFloat("_AuroraWaveSpeed", 1.0f);
				auroraMaterial.SetFloat("_AuroraWaveFrequency", 8.0f);
				AssetDatabase.CreateAsset(auroraMaterial, AURORA_MATERIAL_PATH);
				EditorUtility.SetDirty(auroraMaterial);
				Debug.Log($"[ShaderModdingSDK] Created sample Material {AURORA_MATERIAL_PATH}");
			}

			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
		}

		private static void CreateCartoonWaterSample()
		{
			if (Directory.Exists(WATER_FOLDER) == false)
				Directory.CreateDirectory(WATER_FOLDER);

			// manifest 먼저 (shader import 무관) — aurora-sky 패턴 정합.
			if (File.Exists(WATER_MANIFEST_PATH) == false)
			{
				string waterManifest = @"{
  ""schemaVersion"": 1,
  ""name"": ""Cartoon Water"",
  ""author"": ""Mascari4615"",
  ""version"": ""0.1.0"",
  ""description"": ""Water sample — flat 카툰 톤 + sin wave 파동 + 흰 거품 (foam) + _WMSkyHorizon 옵션 mix"",
  ""bundleFile"": ""cartoon-water.shaderbundle"",
  ""slots"": [
    {
      ""id"": ""water"",
      ""assetName"": ""CartoonWaterMaterial"",
      ""blendMode"": ""replace"",
      ""priority"": 0
    }
  ]
}";
				File.WriteAllText(WATER_MANIFEST_PATH, waterManifest);
				AssetDatabase.ImportAsset(WATER_MANIFEST_PATH);
				Debug.Log($"[ShaderModdingSDK] Created sample manifest {WATER_MANIFEST_PATH}");
			}

			// Material — shader import 끝나야. 매 Reload 시 *코드 default 와 비교 → mismatch 시 reset*.
			// sample 정본 = 코드 default. 모더는 자기 셰이더팩 폴더에서 tweak (sample 폴더 X).
			// 값 변경 시점에만 dirty mark → AssetDatabase 비용 회피 (idempotent).
			Material waterMaterial = AssetDatabase.LoadAssetAtPath<Material>(WATER_MATERIAL_PATH);
			Shader waterShader = AssetDatabase.LoadAssetAtPath<Shader>(WATER_SHADER_PATH);
			if (waterShader == null)
			{
				// shader 가 아직 import 안 됨 — 다음 Domain Reload 에서 재시도.
				AssetDatabase.SaveAssets();
				return;
			}

			bool materialCreated = false;
			if (waterMaterial == null)
			{
				waterMaterial = new Material(waterShader);
				AssetDatabase.CreateAsset(waterMaterial, WATER_MATERIAL_PATH);
				materialCreated = true;
			}

			bool propertiesChanged = false;
			propertiesChanged |= EnsureMaterialColor(waterMaterial, "_DeepColor", new Color(0.05f, 0.30f, 0.45f, 1.0f));
			propertiesChanged |= EnsureMaterialColor(waterMaterial, "_ShallowColor", new Color(0.40f, 0.85f, 0.95f, 1.0f));
			propertiesChanged |= EnsureMaterialFloat(waterMaterial, "_DepthBlend", 0.6f);
			propertiesChanged |= EnsureMaterialColor(waterMaterial, "_FoamColor", new Color(1.0f, 1.0f, 1.0f, 1.0f));
			propertiesChanged |= EnsureMaterialFloat(waterMaterial, "_FoamIntensity", 1.0f);
			propertiesChanged |= EnsureMaterialFloat(waterMaterial, "_FoamThreshold", 0.65f);
			propertiesChanged |= EnsureMaterialFloat(waterMaterial, "_FoamSoftness", 0.08f);
			propertiesChanged |= EnsureMaterialFloat(waterMaterial, "_WaveAmount", 0.06f);
			propertiesChanged |= EnsureMaterialFloat(waterMaterial, "_WaveSpeed", 1.2f);
			propertiesChanged |= EnsureMaterialFloat(waterMaterial, "_WaveFrequency", 1.5f);
			propertiesChanged |= EnsureMaterialFloat(waterMaterial, "_SkyTintAmount", 0.25f);

			if (materialCreated)
				Debug.Log($"[ShaderModdingSDK] Created sample Material {WATER_MATERIAL_PATH}");
			else if (propertiesChanged)
				Debug.Log($"[ShaderModdingSDK] cartoon-water Material reset to code defaults");

			if (materialCreated || propertiesChanged)
			{
				EditorUtility.SetDirty(waterMaterial);
				AssetDatabase.SaveAssets();

				// 자산 변경 시 자동 Build + persistentDataPath install. delayCall = 다음 Editor frame
				// (InitializeOnLoadMethod 시점 BuildPipeline.BuildAssetBundles 안전성 회피).
				EditorApplication.delayCall += ShaderPackBuilder.BuildCartoonWaterSample;
			}

			AssetDatabase.Refresh();
		}

		// Material 의 Float / Color property 가 expected 와 다르면 SetFloat/SetColor + true 반환.
		// 같으면 no-op + false 반환 — AssetDatabase dirty mark 회피.
		private static bool EnsureMaterialFloat(Material material, string propertyName, float expected)
		{
			if (Mathf.Approximately(material.GetFloat(propertyName), expected))
				return false;
			material.SetFloat(propertyName, expected);
			return true;
		}

		private static bool EnsureMaterialColor(Material material, string propertyName, Color expected)
		{
			Color current = material.GetColor(propertyName);
			if (Mathf.Approximately(current.r, expected.r)
				&& Mathf.Approximately(current.g, expected.g)
				&& Mathf.Approximately(current.b, expected.b)
				&& Mathf.Approximately(current.a, expected.a))
				return false;
			material.SetColor(propertyName, expected);
			return true;
		}
	}
}
