using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.Universal;

namespace WitchMendokusai
{
	// CustomBlurOutput.renderTexture asset 자동 생성 + URP Renderer Asset 의 CustomBlurFeature.targetRT 자동 wire.
	// Domain Reload 시 missing 검사 + 누락이면 자동 생성. (TASK-WM-077 Phase B)
	public static class CustomBlurOutputBootstrapMenu
	{
		private const string RT_PATH = "Assets/_WitchMendokusai/Core/Resources/Rendering/CustomBlurOutput.renderTexture";
		private const int RT_WIDTH = 960;
		private const int RT_HEIGHT = 540;

		[InitializeOnLoadMethod]
		private static void AutoBootstrapIfMissing()
		{
			if (AssetDatabase.LoadAssetAtPath<RenderTexture>(RT_PATH) == null)
			{
				CreateBootstrap();
				return;
			}

			EnsureRendererWired();
		}

		[MenuItem("WM/Setup/Recreate CustomBlurOutput Bootstrap")]
		private static void RecreateMenuItem() => CreateBootstrap();

		private static void CreateBootstrap()
		{
			string directory = System.IO.Path.GetDirectoryName(RT_PATH);
			if (System.IO.Directory.Exists(directory) == false)
			{
				System.IO.Directory.CreateDirectory(directory);
			}

			RenderTexture renderTexture = new RenderTexture(RT_WIDTH, RT_HEIGHT, 0, GraphicsFormat.B10G11R11_UFloatPack32)
			{
				name = "CustomBlurOutput",
				useMipMap = false,
				autoGenerateMips = false,
				wrapMode = TextureWrapMode.Clamp,
				filterMode = FilterMode.Bilinear,
			};

			AssetDatabase.CreateAsset(renderTexture, RT_PATH);
			// 전체 SaveAssets 금지. 재로드 경로라 남의 미저장 자산까지 저장함
			AssetDatabase.SaveAssetIfDirty(renderTexture);
			AssetDatabase.Refresh();

			Debug.Log($"[CustomBlurOutputBootstrap] Created {RT_PATH}");

			EnsureRendererWired();
		}

		// URP Renderer Asset 들 안 CustomBlurFeature 인스턴스 찾아 targetRT 자동 wire. idempotent.
		private static void EnsureRendererWired()
		{
			RenderTexture renderTexture = AssetDatabase.LoadAssetAtPath<RenderTexture>(RT_PATH);
			if (renderTexture == null)
			{
				return;
			}

			string[] guids = AssetDatabase.FindAssets("t:UniversalRendererData");
			foreach (string guid in guids)
			{
				string path = AssetDatabase.GUIDToAssetPath(guid);
				UniversalRendererData rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
				if (rendererData == null)
				{
					continue;
				}

				bool changed = false;
				foreach (ScriptableRendererFeature feature in rendererData.rendererFeatures)
				{
					if (feature is CustomBlurFeature blurFeature)
					{
						SerializedObject serializedObject = new SerializedObject(blurFeature);
						SerializedProperty targetRTProp = serializedObject.FindProperty("targetRT");
						if (targetRTProp == null)
						{
							continue;
						}

						if (targetRTProp.objectReferenceValue != renderTexture)
						{
							targetRTProp.objectReferenceValue = renderTexture;
							serializedObject.ApplyModifiedProperties();
							EditorUtility.SetDirty(blurFeature);
							changed = true;
							Debug.Log($"[CustomBlurOutputBootstrap] Wired targetRT to CustomBlurFeature in {path}");
						}
					}
				}

				// 바꾼 renderer 만 저장. 전체 SaveAssets 는 도메인 재로드마다 사용자의 미저장 자산까지 저장하고,
				// 고장 스크립트가 든 dirty 프리팹 (Stage_TempCombat) 에서 저장 오류를 냄 (2026-11-21 MOIS Editor.log 18회)
				if (changed == true)
				{
					EditorUtility.SetDirty(rendererData);
					AssetDatabase.SaveAssetIfDirty(rendererData);
				}
			}
		}
	}
}
