using UnityEditor;
using UnityEngine;

namespace WitchMendokusai
{
	// Sample 폴더 자산 변경 자동 detect → AssetBundle 자동 Build + persistentDataPath install.
	// 사용자 흐름: main pull → Editor focus → reimport → AssetPostprocessor 자동 트리거 → Build → install.
	// → 사용자 클릭 0 (Recreate / Build 메뉴 수동 호출 필요 X). Play Mode 진입 시 자동 RestoreActivePack 으로 적용.
	//
	// 트리거 조건: .shader 또는 manifest.json 변경만. Material .asset 변경은 트리거 X
	// (모더가 인스펙터 tweak 시 자동 빌드 = 작업 흐름 방해 ↑ — 명시 빌드 메뉴 별도 사용).
	public class ShaderPackSamplePostprocessor : AssetPostprocessor
	{
		private const string COZY_NIGHT_PATH = "Assets/_WitchMendokusai/ShaderModdingSDK/Samples/cozy-night/";
		private const string AURORA_SKY_PATH = "Assets/_WitchMendokusai/ShaderModdingSDK/Samples/aurora-sky/";
		private const string CARTOON_WATER_PATH = "Assets/_WitchMendokusai/ShaderModdingSDK/Samples/cartoon-water/";

		private static void OnPostprocessAllAssets(
			string[] importedAssets,
			string[] deletedAssets,
			string[] movedAssets,
			string[] movedFromAssetPaths)
		{
			bool cozyChanged = false;
			bool auroraChanged = false;
			bool waterChanged = false;

			foreach (string assetPath in importedAssets)
			{
				if (ShouldTriggerRebuild(assetPath) == false)
					continue;

				if (assetPath.StartsWith(COZY_NIGHT_PATH))
					cozyChanged = true;
				else if (assetPath.StartsWith(AURORA_SKY_PATH))
					auroraChanged = true;
				else if (assetPath.StartsWith(CARTOON_WATER_PATH))
					waterChanged = true;
			}

			if (cozyChanged)
			{
				Debug.Log($"[ShaderPackSamplePostprocessor] cozy-night sample changed → auto Build + install.");
				ShaderPackBuilder.BuildCozyNightSample();
			}

			if (auroraChanged)
			{
				Debug.Log($"[ShaderPackSamplePostprocessor] aurora-sky sample changed → auto Build + install.");
				ShaderPackBuilder.BuildAuroraSkySample();
			}

			if (waterChanged)
			{
				Debug.Log($"[ShaderPackSamplePostprocessor] cartoon-water sample changed → auto Build + install.");
				ShaderPackBuilder.BuildCartoonWaterSample();
			}
		}

		// .shader 또는 manifest.json 변경만 트리거 — Material .asset 인스펙터 tweak 은 보호.
		private static bool ShouldTriggerRebuild(string assetPath)
		{
			return assetPath.EndsWith(".shader") || assetPath.EndsWith("manifest.json");
		}
	}
}
