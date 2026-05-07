using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace WitchMendokusai
{
	// cozy-night 샘플 셰이더팩 자동 생성. Domain Reload 시 missing 검사 + 누락이면 자동. (TASK-WM-055 P1-F)
	// 모더가 자기 셰이더팩 만들 때 이 폴더를 복사해서 시작점으로 사용.
	public static class ShaderModdingSDKBootstrap
	{
		private const string SAMPLE_FOLDER = "Assets/_WitchMendokusai/ShaderModdingSDK/Samples/cozy-night";
		private const string PROFILE_ASSET_PATH = SAMPLE_FOLDER + "/CozyNightVolumeProfile.asset";
		private const string MANIFEST_PATH = SAMPLE_FOLDER + "/manifest.json";

		[InitializeOnLoadMethod]
		private static void AutoBootstrapIfMissing()
		{
			if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(PROFILE_ASSET_PATH) != null && File.Exists(MANIFEST_PATH))
				return;

			CreateSample();
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
	}
}
