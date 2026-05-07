using System.IO;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai
{
	// 셰이더팩 빌드 — VolumeProfile.asset 을 AssetBundle (.shaderbundle) 로 출력 + manifest.json 같이 묶음.
	// 출력 위치 = {repo_root}/Shaderpacks_Output/{packId}/  (repo .gitignore 권장 — follow-up)
	// 사용자는 그 폴더 내용을 {persistentDataPath}/shaderpacks/{packId}/ 로 복사.
	public static class ShaderPackBuilder
	{
		private const string OUTPUT_FOLDER_NAME = "Shaderpacks_Output";
		private const string SAMPLE_PACK_PATH = "Assets/_WitchMendokusai/ShaderModdingSDK/Samples/cozy-night";
		private const string SAMPLE_PACK_ID = "cozy-night";
		private const string SAMPLE_BUNDLE_FILE = "cozy-night.shaderbundle";

		[MenuItem("WM/ShaderModdingSDK/Build cozy-night Sample")]
		public static void BuildCozyNightSample()
		{
			BuildShaderPack(SAMPLE_PACK_ID, SAMPLE_PACK_PATH, SAMPLE_BUNDLE_FILE);
		}

		public static void BuildShaderPack(string packId, string sourceFolderPath, string bundleFileName)
		{
			string repoRoot = Directory.GetParent(Application.dataPath).FullName;
			string outputRoot = Path.Combine(repoRoot, OUTPUT_FOLDER_NAME, packId);
			if (Directory.Exists(outputRoot))
				Directory.Delete(outputRoot, true);
			Directory.CreateDirectory(outputRoot);

			string[] assetGuids = AssetDatabase.FindAssets("t:VolumeProfile", new[] { sourceFolderPath });
			if (assetGuids.Length == 0)
			{
				Debug.LogError($"[ShaderPackBuilder] No VolumeProfile in {sourceFolderPath}");
				return;
			}

			string assetPath = AssetDatabase.GUIDToAssetPath(assetGuids[0]);

			AssetBundleBuild build = new AssetBundleBuild
			{
				assetBundleName = packId,
				assetNames = new[] { assetPath }
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

			Debug.Log($"[ShaderPackBuilder] Built {packId} → {outputRoot}");
			Debug.Log($"[ShaderPackBuilder] Drop into: {Application.persistentDataPath}/shaderpacks/{packId}/");
			EditorUtility.RevealInFinder(outputRoot);
		}
	}
}
