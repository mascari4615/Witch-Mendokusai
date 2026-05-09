using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace WitchMendokusai
{
	// 샘플 셰이더팩 자동 생성. Domain Reload 시 missing 검사 + 누락이면 자동. (TASK-WM-055 P1-F + WM-058 P2-D)
	// 모더가 자기 셰이더팩 만들 때 이 폴더를 복사해서 시작점으로 사용.
	//   - cozy-night : PostProcess 슬롯 (P1)
	//   - aurora-sky : Skybox 슬롯 (P2-D) — uniform contract 시연
	public static class ShaderModdingSDKBootstrap
	{
		private const string SAMPLE_FOLDER = "Assets/_WitchMendokusai/ShaderModdingSDK/Samples/cozy-night";
		private const string PROFILE_ASSET_PATH = SAMPLE_FOLDER + "/CozyNightVolumeProfile.asset";
		private const string MANIFEST_PATH = SAMPLE_FOLDER + "/manifest.json";

		private const string AURORA_FOLDER = "Assets/_WitchMendokusai/ShaderModdingSDK/Samples/aurora-sky";
		private const string AURORA_SHADER_PATH = AURORA_FOLDER + "/AuroraSky.shader";
		private const string AURORA_MATERIAL_PATH = AURORA_FOLDER + "/AuroraSkyMaterial.mat";
		private const string AURORA_MANIFEST_PATH = AURORA_FOLDER + "/manifest.json";

		[InitializeOnLoadMethod]
		private static void AutoBootstrapIfMissing()
		{
			if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(PROFILE_ASSET_PATH) == null || File.Exists(MANIFEST_PATH) == false)
				CreateSample();

			if (AssetDatabase.LoadAssetAtPath<Material>(AURORA_MATERIAL_PATH) == null || File.Exists(AURORA_MANIFEST_PATH) == false)
				CreateAuroraSkySample();
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

		private static void CreateSample()
		{
			if (Directory.Exists(SAMPLE_FOLDER) == false)
				Directory.CreateDirectory(SAMPLE_FOLDER);

			VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PROFILE_ASSET_PATH);
			if (profile == null)
			{
				profile = ScriptableObject.CreateInstance<VolumeProfile>();
				AssetDatabase.CreateAsset(profile, PROFILE_ASSET_PATH);

				// 명백히 눈에 보이는 강도 — 검증용 sample (모더는 자기 셰이더팩에서 자유 조정)
				// VolumeComponent 들은 sub-asset 으로 추가 (URP Editor 패턴 — profile.Add<T> 만으론 .asset 저장 안 됨).
				ColorAdjustments colorAdjust = ScriptableObject.CreateInstance<ColorAdjustments>();
				colorAdjust.hideFlags = HideFlags.HideInHierarchy;
				colorAdjust.postExposure.overrideState = true;
				colorAdjust.postExposure.value = 0.5f;
				colorAdjust.colorFilter.overrideState = true;
				colorAdjust.colorFilter.value = new Color(1.0f, 0.65f, 0.45f, 1.0f);
				colorAdjust.saturation.overrideState = true;
				colorAdjust.saturation.value = 30f;
				AssetDatabase.AddObjectToAsset(colorAdjust, profile);
				profile.components.Add(colorAdjust);

				Vignette vignette = ScriptableObject.CreateInstance<Vignette>();
				vignette.hideFlags = HideFlags.HideInHierarchy;
				vignette.intensity.overrideState = true;
				vignette.intensity.value = 0.5f;
				vignette.color.overrideState = true;
				vignette.color.value = new Color(0.2f, 0.0f, 0.3f, 1.0f);
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

			Material auroraMaterial = AssetDatabase.LoadAssetAtPath<Material>(AURORA_MATERIAL_PATH);
			if (auroraMaterial == null)
			{
				Shader auroraShader = AssetDatabase.LoadAssetAtPath<Shader>(AURORA_SHADER_PATH);
				if (auroraShader == null)
				{
					// shader 가 아직 import 안 됨 — 다음 Domain Reload 에서 재시도. silent skip.
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

			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
		}
	}
}
