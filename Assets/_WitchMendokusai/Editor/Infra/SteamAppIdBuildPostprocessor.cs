#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace WitchMendokusai
{
	// Windows 64비트 빌드 산출물 (exe) 옆에 steam_appid.txt 를 SteamSettings 값으로 생성.
	//
	// 정본: App ID 는 SteamSettings.asset 한 곳. 이 파일은 그 값의 사본이라 저장소에 두지 않음.
	// 용도: Steam 클라이언트를 거치지 않고 exe 를 직접 실행하는 dev, 시험 실행의 안전망.
	//   SteamClient.Init(appId) 가 SteamAppId 환경 변수도 직접 세우지만, steam_api 가 작업 폴더의
	//   이 파일을 먼저 찾는 경로가 있어 같이 둠.
	// Steam 클라이언트로 실행한 정식 빌드는 Steam 이 App ID 를 주입하므로 이 파일은 무시됨.
	// Facepunch.Steamworks 는 Win64 플러그인만 켜져 있어 그 밖의 대상은 건너뜀.
	public sealed class SteamAppIdBuildPostprocessor : IPostprocessBuildWithReport
	{
		private const string STEAM_APPID_FILENAME = "steam_appid.txt";

		public int callbackOrder => 0;

		public void OnPostprocessBuild(BuildReport report)
		{
			if (report.summary.platform != BuildTarget.StandaloneWindows64)
			{
				return;
			}

			string outputPath = report.summary.outputPath;
			string buildDirectory = Path.GetDirectoryName(outputPath);
			if (string.IsNullOrEmpty(buildDirectory))
			{
				throw new BuildFailedException("[SteamAppId] 빌드 출력 경로 파싱 실패: " + outputPath);
			}

			uint appId = SteamSettings.Load().AppId;
			string destinationPath = Path.Combine(buildDirectory, STEAM_APPID_FILENAME);
			File.WriteAllText(destinationPath, appId.ToString());
			Debug.Log("[SteamAppId] " + STEAM_APPID_FILENAME + " (" + appId + ") -> " + destinationPath);
		}
	}
}
#endif
