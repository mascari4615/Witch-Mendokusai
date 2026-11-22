using System;
using Steamworks;
using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// Steam 클라이언트 연결 하나 (Facepunch.Steamworks).
	///
	/// - 컴파일 범위: 에디터와 Windows 64비트 빌드만 (asmdef includePlatforms). 모바일은 어셈블리째 없음
	/// - 스스로 섬: 씬 배치나 로비 경유 없이 부팅 직후 생성 (WorldLinkProvider 와 같은 모양)
	/// - 콜백: asyncCallbacks 끔, 매 프레임 메인 스레드에서 RunCallbacks. 콜백 안 Unity API 사용 안전
	/// - 실패: Steam 미실행, 미소유, dll 없음이면 경고 한 줄 뒤 Steam 없이 진행. 게임 진행은 Steam 과 무관
	/// - 사용 쪽: 씬 로드 전에 연결이 끝나므로 이벤트 대신 IsReady 를 사용 시점에 확인
	/// </summary>
	public sealed class SteamService : MonoBehaviour
	{
		public static SteamService Instance { get; private set; }
		public static bool IsReady => Instance != null && Instance.isReady;

		private bool isReady;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
		private static void StandUp()
		{
			if (Instance != null)
			{
				return;
			}

			// CI, 헤드리스 검증은 Steam 클라이언트 없는 자리
			if (Application.isBatchMode)
			{
				return;
			}

			SteamSettings settings = SteamSettings.Load();
			if (Application.isEditor && settings.InitializeInEditor == false)
			{
				return;
			}

			GameObject holder = new GameObject(nameof(SteamService));
			DontDestroyOnLoad(holder);
			SteamService service = holder.AddComponent<SteamService>();
			service.Connect(settings.AppId);
		}

		private void Connect(uint appId)
		{
			Instance = this;
			try
			{
				SteamClient.Init(appId, false);
			}
			catch (Exception exception)
			{
				Debug.LogWarning("[Steam] 연결 실패, Steam 기능 없이 진행: " + exception.Message);
				return;
			}

			isReady = true;
			Debug.Log("[Steam] 연결 " + SteamClient.Name + " (" + SteamClient.SteamId + "), 앱 " + appId);
		}

		private void Update()
		{
			if (isReady)
			{
				SteamClient.RunCallbacks();
			}
		}

		private void OnApplicationQuit() => Disconnect();

		private void OnDestroy()
		{
			Disconnect();
			if (Instance == this)
			{
				Instance = null;
			}
		}

		private void Disconnect()
		{
			if (isReady == false)
			{
				return;
			}

			isReady = false;
			SteamClient.Shutdown();
		}
	}
}
