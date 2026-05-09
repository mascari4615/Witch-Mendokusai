using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai
{
	// 셰이더팩 빌드 — 폴더 안 모든 자산 (VolumeProfile / Material / Shader / etc.) 을
	// AssetBundle (.shaderbundle) 로 출력 + manifest.json 같이 묶음.
	// 출력 위치 = {repo_root}/Shaderpacks_Output/{packId}/  (모더 배포용 zip 만들 때 사용)
	// 자동 install = {persistentDataPath}/shaderpacks/{packId}/ 에 자동 복사 → 사용자 수동 복사 X.
	// (모더 다른 사용자에게 배포 시점에는 출력 폴더 zip → 사용자 copy 흐름)
	public static class ShaderPackBuilder
	{
		private const string OUTPUT_FOLDER_NAME = "Shaderpacks_Output";

		private const string COZY_NIGHT_PATH = "Assets/_WitchMendokusai/ShaderModdingSDK/Samples/cozy-night";
		private const string COZY_NIGHT_ID = "cozy-night";
		private const string COZY_NIGHT_BUNDLE = "cozy-night.shaderbundle";

		private const string AURORA_SKY_PATH = "Assets/_WitchMendokusai/ShaderModdingSDK/Samples/aurora-sky";
		private const string AURORA_SKY_ID = "aurora-sky";
		private const string AURORA_SKY_BUNDLE = "aurora-sky.shaderbundle";

		private const string CARTOON_WATER_PATH = "Assets/_WitchMendokusai/ShaderModdingSDK/Samples/cartoon-water";
		private const string CARTOON_WATER_ID = "cartoon-water";
		private const string CARTOON_WATER_BUNDLE = "cartoon-water.shaderbundle";

		[MenuItem("WM/ShaderModdingSDK/Build cozy-night Sample")]
		public static void BuildCozyNightSample()
		{
			BuildShaderPack(COZY_NIGHT_ID, COZY_NIGHT_PATH, COZY_NIGHT_BUNDLE);
		}

		[MenuItem("WM/ShaderModdingSDK/Build aurora-sky Sample")]
		public static void BuildAuroraSkySample()
		{
			BuildShaderPack(AURORA_SKY_ID, AURORA_SKY_PATH, AURORA_SKY_BUNDLE);
		}

		[MenuItem("WM/ShaderModdingSDK/Build cartoon-water Sample")]
		public static void BuildCartoonWaterSample()
		{
			BuildShaderPack(CARTOON_WATER_ID, CARTOON_WATER_PATH, CARTOON_WATER_BUNDLE);
		}

		public static void BuildShaderPack(string packId, string sourceFolderPath, string bundleFileName)
		{
			string repoRoot = Directory.GetParent(Application.dataPath).FullName;
			string outputRoot = Path.Combine(repoRoot, OUTPUT_FOLDER_NAME, packId);
			if (Directory.Exists(outputRoot))
				Directory.Delete(outputRoot, true);
			Directory.CreateDirectory(outputRoot);

			List<string> assetPaths = CollectBundleAssets(sourceFolderPath);
			if (assetPaths.Count == 0)
			{
				Debug.LogError($"[ShaderPackBuilder] No bundleable asset in {sourceFolderPath}");
				return;
			}

			AssetBundleBuild build = new AssetBundleBuild
			{
				assetBundleName = packId,
				assetNames = assetPaths.ToArray()
			};

			string tempBuildFolder = Path.Combine(outputRoot, "_build");
			Directory.CreateDirectory(tempBuildFolder);

			BuildTarget activeBuildTarget = EditorUserBuildSettings.activeBuildTarget;
			BuildPipeline.BuildAssetBundles(tempBuildFolder, new[] { build }, BuildAssetBundleOptions.None, activeBuildTarget);

			string sourceBundlePath = Path.Combine(tempBuildFolder, packId);
			string targetBundlePath = Path.Combine(outputRoot, bundleFileName);
			if (File.Exists(targetBundlePath))
				File.Delete(targetBundlePath);
			File.Move(sourceBundlePath, targetBundlePath);

			string manifestSource = Path.Combine(sourceFolderPath, "manifest.json");
			string manifestTarget = Path.Combine(outputRoot, "manifest.json");
			File.Copy(manifestSource, manifestTarget, true);

			Directory.Delete(tempBuildFolder, true);

			Debug.Log($"[ShaderPackBuilder] Built {packId} ({assetPaths.Count} asset{(assetPaths.Count == 1 ? "" : "s")}) → {outputRoot}");

			AutoInstall(packId, targetBundlePath, manifestTarget, bundleFileName);

			EditorUtility.RevealInFinder(outputRoot);
		}

		// 빌드 직후 {persistentDataPath}/shaderpacks/{packId}/ 로 manifest + bundle 자동 복사.
		// 사용자 수동 폴더 복사 단계 제거 — Build 메뉴 클릭만으로 즉시 적용 가능.
		// 게임 실행 중이면 bundle file lock 으로 fail — 명시적 안내 후 사용자 Revert 또는 Stop 후 재시도.
		private static void AutoInstall(string packId, string sourceBundlePath, string sourceManifestPath, string bundleFileName)
		{
			string installRoot = Path.Combine(Application.persistentDataPath, "shaderpacks", packId);
			string installBundlePath = Path.Combine(installRoot, bundleFileName);
			string installManifestPath = Path.Combine(installRoot, "manifest.json");

			try
			{
				if (Directory.Exists(installRoot) == false)
					Directory.CreateDirectory(installRoot);

				File.Copy(sourceBundlePath, installBundlePath, true);
				File.Copy(sourceManifestPath, installManifestPath, true);

				Debug.Log($"[ShaderPackBuilder] Auto-installed → {installRoot}");
				Debug.Log($"[ShaderPackBuilder] Game restart 또는 ESC > 환경설정 > 쉐이더팩 탭 > 재스캔 → 적용");
			}
			catch (IOException exception)
			{
				Debug.LogError($"[ShaderPackBuilder] Auto-install fail (file locked?) — '{packId}' 활성 중이면 Revert 후 재시도, 또는 게임 Stop. {exception.Message}");
			}
		}

		// 폴더 안 모든 자산 수집 — 폴더 / manifest.json / readme.md 제외.
		// Material 만 박으면 의존 shader 자동 포함되지만, 모더가 명시적 자산을 자유 추가하게 generic 검색.
		private static List<string> CollectBundleAssets(string folderPath)
		{
			string[] guids = AssetDatabase.FindAssets("", new[] { folderPath });
			List<string> assetPaths = new List<string>();
			foreach (string guid in guids)
			{
				string path = AssetDatabase.GUIDToAssetPath(guid);
				if (AssetDatabase.IsValidFolder(path))
					continue;
				if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
					continue;
				if (path.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
					continue;
				assetPaths.Add(path);
			}
			return assetPaths;
		}
	}
}
