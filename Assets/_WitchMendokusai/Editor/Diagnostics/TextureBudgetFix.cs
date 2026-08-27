using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai.EditorTools
{
	/// <summary>
	/// <b>압축이 꺼진 채 구워지는 텍스처</b>를 찾아 켠다 (TASK-WM-409 단계 C).
	///
	/// ★ 실측 2026-08-17: `UI_FadeCircle.png` 은 원본이 <b>97KB</b> 인데 빌드에서는 <b>4.0MB</b> 였다.
	///   PNG 는 디스크에서만 압축돼 있고, 빌드에는 <b>GPU 포맷</b>으로 다시 구워진다 —
	///   포맷을 안 정하면 RGBA32 로 굽는다.
	///   ⚠ 더 고약한 것: <b>압축을 켜도 안 먹는 경우가 있다</b> — 실측 2026-08-17,
	///     이 그림은 <b>1028×994</b> 로 2의 거듭제곱이 아니라 DXT/크런치가 <b>불가능</b>했다.
	///     설정만 바꾸고 「켰다」고 보고하면 거짓말이 된다 — 그래서 <c>npotScale</c> 로
	///     가장 가까운 2의 거듭제곱(1024)으로 맞춘다. 그제야 압축이 실제로 먹는다.
	///
	/// 하는 일: 대상 텍스처의 임포트 설정을 <c>Automatic(압축)</c> 으로 바꾼다.
	///   해상도는 <b>안 건드린다</b> — 줄이면 눈에 보이고, 그건 사용자 영역이다.
	///   압축만으로 4배가 준다.
	/// </summary>
	public static class TextureBudgetFix
	{
		private const string TAG = "[텍스처예산]";

		/// <summary>압축이 꺼져 있는데 큰 것들 — 인벤토리에서 확인된 것부터.</summary>
		private static readonly string[] TARGETS = new string[]
		{
			"Assets/_WitchMendokusai/Core/Scripts/UI/Common/Sprites/UI_FadeCircle.png",
		};

		[MenuItem("WM/Migrate/Enable Texture Compression (TASK-WM-409 C)")]
		public static void Run()
		{
			List<string> fixedOnes = new List<string>();
			foreach (string path in TARGETS)
			{
				TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
				if (importer == null)
				{
					Debug.LogError(TAG + " 텍스처를 못 찾았다 — " + path);
					continue;
				}

				TextureImporterPlatformSettings standalone = importer.GetPlatformTextureSettings("Standalone");
				standalone.overridden = true;
				standalone.format = TextureImporterFormat.Automatic;
				standalone.textureCompression = TextureImporterCompression.Compressed;
				importer.SetPlatformTextureSettings(standalone);

				// ★ 2의 거듭제곱이 아니면 압축 자체가 안 걸린다 — 먼저 그것부터 맞춘다.
				importer.npotScale = TextureImporterNPOTScale.ToNearest;
				importer.textureCompression = TextureImporterCompression.Compressed;
				importer.crunchedCompression = true;
				importer.compressionQuality = 50;

				EditorUtility.SetDirty(importer);
				importer.SaveAndReimport();
				fixedOnes.Add(path);
				Debug.Log(TAG + " 압축 켬 — " + path);
			}
			AssetDatabase.Refresh();
			Debug.Log(TAG + " 완료 — " + fixedOnes.Count + "개. 다음은 빌드 인벤토리로 확인할 것");
		}
	}
}
