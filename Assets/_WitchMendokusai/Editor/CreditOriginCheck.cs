using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 크레딧 자산과 저장소 파일의 어긋남 검사.
	/// 1. 항목 필수 필드 2. AssetPaths 실재 3. .origin 파일의 credit 와 license 4. 라이선스 파일 묶음의 크레딧 등록 5. 파일 머리 origin: 줄의 크레딧 범위.
	/// 메뉴 WM/Credits/Check Origins, .origin 과 라이선스 파일 임포트 때 자동, EditMode 시험 CreditsInfoTest
	/// </summary>
	public static class CreditOriginCheck
	{
		// 라이선스 파일과 origin 머리줄을 찾는 범위 (프로젝트 루트 기준)
		public static readonly string[] SCAN_ROOTS = { "Assets/_WitchMendokusai", "Assets/Plugins", "Assets/TextMesh Pro", "FMODAssets/Assets" };
		private static readonly string[] LICENSE_PREFIXES = { "license", "licence", "copying", "ofl-", "ofl.", "ofl_" };
		private static readonly string[] LICENSE_EXTENSIONS = { "", ".txt", ".md" };
		private static readonly string[] HEADER_EXTENSIONS = { ".obj", ".mtl", ".gltf", ".svg", ".ply" };
		private const int HEADER_LINE_COUNT = 3;

		public static string ProjectRoot => Path.GetDirectoryName(Application.dataPath).Replace('\\', '/');

		public static CreditsInfo FindCredits()
		{
			string[] guids = AssetDatabase.FindAssets("t:" + nameof(CreditsInfo));
			if (guids.Length != 1)
				throw new InvalidOperationException($"CreditsInfo 자산은 하나여야 함. 찾은 수 {guids.Length}");
			return AssetDatabase.LoadAssetAtPath<CreditsInfo>(AssetDatabase.GUIDToAssetPath(guids[0]));
		}

		public static List<string> Run(CreditsInfo credits, string projectRoot)
		{
			List<string> problems = credits.Validate();
			problems.AddRange(CheckAssetPaths(credits, projectRoot));

			// 저장소 한 번만 훑기
			foreach (string relativePath in EnumerateFiles(projectRoot))
			{
				if (relativePath.EndsWith(CreditOrigin.FILE_EXTENSION, StringComparison.OrdinalIgnoreCase))
					problems.AddRange(CreditOrigin.Parse(File.ReadAllText(Absolute(projectRoot, relativePath))).Check(credits, relativePath));
				else if (IsLicenseFile(relativePath))
				{
					if (IsCovered(credits, projectRoot, relativePath) == false)
						problems.Add($"{relativePath}: 라이선스 파일이 있는데 크레딧 항목의 AssetPaths 에 없음");
				}
				else if (HasHeaderExtension(relativePath) && HasOriginHeader(Absolute(projectRoot, relativePath)) && IsCovered(credits, projectRoot, relativePath) == false)
					problems.Add($"{relativePath}: 머리에 origin: 줄이 있는데 크레딧 항목이나 .origin 파일이 없음");
			}
			return problems;
		}

		[MenuItem("WM/Credits/Check Origins")]
		public static void CheckFromMenu() => Report(Run(FindCredits(), ProjectRoot), true);

		private static void Report(List<string> problems, bool logPass)
		{
			if (problems.Count == 0)
			{
				if (logPass)
					Debug.Log("[Credits] 크레딧 검사 통과");
				return;
			}
			Debug.LogWarning($"[Credits] 크레딧 어긋남 {problems.Count}건\n" + string.Join("\n", problems));
		}

		private static List<string> CheckAssetPaths(CreditsInfo credits, string projectRoot)
		{
			List<string> problems = new();
			foreach (CreditEntry entry in credits.Entries)
			{
				foreach (string assetPath in entry.AssetPaths)
				{
					string absolute = Absolute(projectRoot, assetPath);
					if (File.Exists(absolute) == false && Directory.Exists(absolute) == false)
						problems.Add($"[{entry.Id}] AssetPaths 의 '{assetPath}' 없음");
				}
			}
			return problems;
		}

		// 항목의 AssetPaths 아래이거나, 같은 폴더나 위 폴더에 .origin 파일
		private static bool IsCovered(CreditsInfo credits, string projectRoot, string relativePath)
		{
			foreach (CreditEntry entry in credits.Entries)
			{
				foreach (string assetPath in entry.AssetPaths)
				{
					string trimmed = assetPath.TrimEnd('/');
					if (string.Equals(relativePath, trimmed, StringComparison.OrdinalIgnoreCase) || relativePath.StartsWith(trimmed + "/", StringComparison.OrdinalIgnoreCase))
						return true;
				}
			}
			for (string folder = Path.GetDirectoryName(relativePath)?.Replace('\\', '/'); string.IsNullOrEmpty(folder) == false; folder = Path.GetDirectoryName(folder)?.Replace('\\', '/'))
			{
				string absoluteFolder = Absolute(projectRoot, folder);
				if (Directory.Exists(absoluteFolder) && Directory.GetFiles(absoluteFolder, "*" + CreditOrigin.FILE_EXTENSION).Length > 0)
					return true;
			}
			return false;
		}

		private static bool IsLicenseFile(string relativePath)
		{
			string fileName = Path.GetFileName(relativePath).ToLowerInvariant();
			if (Array.IndexOf(LICENSE_EXTENSIONS, Path.GetExtension(fileName)) < 0)
				return false;
			foreach (string prefix in LICENSE_PREFIXES)
			{
				if (fileName.StartsWith(prefix, StringComparison.Ordinal))
					return true;
			}
			return false;
		}

		private static bool HasHeaderExtension(string relativePath)
			=> Array.IndexOf(HEADER_EXTENSIONS, Path.GetExtension(relativePath).ToLowerInvariant()) >= 0;

		private static bool HasOriginHeader(string absolutePath)
		{
			using StreamReader reader = new StreamReader(absolutePath);
			for (int i = 0; i < HEADER_LINE_COUNT; i++)
			{
				string line = reader.ReadLine();
				if (line == null)
					return false;
				if (line.IndexOf("origin:", StringComparison.OrdinalIgnoreCase) >= 0)
					return true;
			}
			return false;
		}

		private static IEnumerable<string> EnumerateFiles(string projectRoot)
		{
			foreach (string scanRoot in SCAN_ROOTS)
			{
				string absoluteRoot = Absolute(projectRoot, scanRoot);
				if (Directory.Exists(absoluteRoot) == false)
					continue;
				foreach (string absolute in Directory.EnumerateFiles(absoluteRoot, "*", SearchOption.AllDirectories))
				{
					string relative = scanRoot + "/" + absolute.Substring(absoluteRoot.Length + 1).Replace(Path.DirectorySeparatorChar, '/');
					if (relative.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) == false)
						yield return relative;
				}
			}
		}

		private static string Absolute(string projectRoot, string relativePath) => projectRoot + "/" + relativePath;

		// .origin 이나 라이선스 파일이 들어오면 바로 검사, 어긋남만 경고
		private class ImportWatcher : AssetPostprocessor
		{
			private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
			{
				bool relevant = false;
				foreach (string path in importedAssets)
				{
					if (path.EndsWith(CreditOrigin.FILE_EXTENSION, StringComparison.OrdinalIgnoreCase) || IsLicenseFile(path))
					{
						relevant = true;
						break;
					}
				}
				if (relevant == false)
					return;
				string[] guids = AssetDatabase.FindAssets("t:" + nameof(CreditsInfo));
				if (guids.Length != 1)
					return;
				CreditsInfo credits = AssetDatabase.LoadAssetAtPath<CreditsInfo>(AssetDatabase.GUIDToAssetPath(guids[0]));
				if (credits == null)
					return;
				Report(Run(credits, ProjectRoot), false);
			}
		}
	}
}
