#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace WitchMendokusai
{
	// steam_appid.txt 를 standalone 빌드 산출물(exe) 옆에 자동 복사한다.
	//
	// 왜: Facepunch.Steamworks 의 SteamClient.Init() 은 working directory 에서
	// steam_appid.txt 를 읽는다. Steam 클라이언트를 거치지 않고 빌드를 직접 실행하는
	// dev/CI 검증(= TASK-WM-100 의 "로컬 Tugboat → Steam 빌드 FishyFacepunch 전환
	// 플로우 검증")에서는 이 파일이 exe 옆에 없으면 Init 이 실패해 Steam transport 가
	// 아예 뜨지 않는다. 에디터 실행은 working dir = 프로젝트 루트라 루트의
	// steam_appid.txt 가 그대로 읽히지만, standalone 빌드는 별도 디렉토리이므로 복사 필요.
	//
	// 정식 출시 빌드를 Steam 클라이언트로 런칭하면 Steam 이 App ID 를 주입하므로
	// 이 파일은 무시되어도 무방하다(= dev 편의 + 비-Steam 실행 안전망 전용).
	public sealed class SteamAppIdBuildPostprocessor : IPostprocessBuildWithReport
	{
		private const string STEAM_APPID_FILENAME = "steam_appid.txt";

		public int callbackOrder => 0;

		public void OnPostprocessBuild(BuildReport report)
		{
			BuildTarget buildTarget = report.summary.platform;
			if (IsStandaloneTarget(buildTarget) == false)
			{
				return;
			}

			string projectRoot = Directory.GetParent(Application.dataPath).FullName;
			string sourcePath = Path.Combine(projectRoot, STEAM_APPID_FILENAME);
			if (File.Exists(sourcePath) == false)
			{
				Debug.LogWarning($"[SteamAppId] 프로젝트 루트에 {STEAM_APPID_FILENAME} 이 없어 복사 스킵. " +
					"Steam 없이 standalone 실행 시 SteamClient.Init() 실패 가능.");
				return;
			}

			string outputPath = report.summary.outputPath;
			string buildDirectory = Path.GetDirectoryName(outputPath);
			if (string.IsNullOrEmpty(buildDirectory))
			{
				Debug.LogWarning($"[SteamAppId] 빌드 출력 경로 파싱 실패: {outputPath}");
				return;
			}

			string destinationPath = Path.Combine(buildDirectory, STEAM_APPID_FILENAME);
			File.Copy(sourcePath, destinationPath, true);
			Debug.Log($"[SteamAppId] {STEAM_APPID_FILENAME} → {destinationPath} 복사 완료.");
		}

		private static bool IsStandaloneTarget(BuildTarget buildTarget)
		{
			return buildTarget == BuildTarget.StandaloneWindows
				|| buildTarget == BuildTarget.StandaloneWindows64
				|| buildTarget == BuildTarget.StandaloneLinux64
				|| buildTarget == BuildTarget.StandaloneOSX;
		}
	}
}
#endif
