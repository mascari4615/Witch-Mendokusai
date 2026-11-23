using UnityEngine;

namespace WitchMendokusai
{
	/// <summary>
	/// 화면 설정 (전체화면, 해상도, 수직 동기, FPS 제한). 값은 PlayerPrefs, 화면 메뉴는 설정 창의 화면 탭.
	/// 바꾸면 바로 적용하고, 부팅 때 한 번 다시 적용해 재시작 뒤에도 남음.
	/// 기본: 전체화면, 모니터 해상도, 수직 동기 끔, FPS 는 모니터 주사율에 맞춤 (데모 목표 144FPS, 사용자 2026-11-23 승인)
	/// </summary>
	public static class DisplaySettings
	{
		private const string FULLSCREEN_KEY = "WM.Display.Fullscreen";
		private const string WIDTH_KEY = "WM.Display.Width";
		private const string HEIGHT_KEY = "WM.Display.Height";
		private const string VSYNC_KEY = "WM.Display.VSync";
		private const string FRAME_RATE_KEY = "WM.Display.FrameRate";

		/// <summary> FPS 제한 값 중 모니터 주사율에 맞춤 </summary>
		public const int FRAME_RATE_MONITOR = 0;
		/// <summary> FPS 제한 값 중 제한 없음. Application.targetFrameRate 의 -1 과 같은 뜻 </summary>
		public const int FRAME_RATE_UNLIMITED = -1;

		public static bool Fullscreen
		{
			get => PlayerPrefs.GetInt(FULLSCREEN_KEY, 1) == 1;
			set
			{
				PlayerPrefs.SetInt(FULLSCREEN_KEY, value ? 1 : 0);
				PlayerPrefs.Save();
				ApplyScreen();
			}
		}

		/// <summary> 고른 적 없으면 모니터 해상도 </summary>
		public static Vector2Int Resolution
		{
			get
			{
				Resolution monitor = Screen.currentResolution;
				return new Vector2Int(PlayerPrefs.GetInt(WIDTH_KEY, monitor.width), PlayerPrefs.GetInt(HEIGHT_KEY, monitor.height));
			}
			set
			{
				PlayerPrefs.SetInt(WIDTH_KEY, value.x);
				PlayerPrefs.SetInt(HEIGHT_KEY, value.y);
				PlayerPrefs.Save();
				ApplyScreen();
			}
		}

		public static bool VSync
		{
			get => PlayerPrefs.GetInt(VSYNC_KEY, 0) == 1;
			set
			{
				PlayerPrefs.SetInt(VSYNC_KEY, value ? 1 : 0);
				PlayerPrefs.Save();
				ApplyFrameRate();
			}
		}

		/// <summary> FPS 숫자, 또는 FRAME_RATE_MONITOR, FRAME_RATE_UNLIMITED </summary>
		public static int FrameRateLimit
		{
			get => PlayerPrefs.GetInt(FRAME_RATE_KEY, FRAME_RATE_MONITOR);
			set
			{
				PlayerPrefs.SetInt(FRAME_RATE_KEY, value);
				PlayerPrefs.Save();
				ApplyFrameRate();
			}
		}

		/// <summary> 설정값을 Application.targetFrameRate 로. 수직 동기가 켜져 있으면 Unity 가 이 값을 무시하므로 제한 없음 </summary>
		public static int ResolveTargetFrameRate(bool vSync, int frameRateLimit, double monitorRefreshRate)
		{
			if (vSync)
				return FRAME_RATE_UNLIMITED;
			if (frameRateLimit == FRAME_RATE_MONITOR)
				return monitorRefreshRate > 0d ? Mathf.RoundToInt((float)monitorRefreshRate) : FRAME_RATE_UNLIMITED;
			return frameRateLimit;
		}

		public static void Apply()
		{
			ApplyScreen();
			ApplyFrameRate();
		}

		private static void ApplyFrameRate()
		{
			// 품질 단계마다 따로 박힌 수직 동기 값을 덮어씀
			QualitySettings.vSyncCount = VSync ? 1 : 0;
			Application.targetFrameRate = ResolveTargetFrameRate(VSync, FrameRateLimit, Screen.currentResolution.refreshRateRatio.value);
		}

		private static void ApplyScreen()
		{
			// 에디터에서는 Game 뷰 크기를 바꾸지 못하고 경고만 남음
			if (Application.isEditor)
				return;
			Vector2Int resolution = Resolution;
			Screen.SetResolution(resolution.x, resolution.y, Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
		}

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
		private static void ApplyOnBoot() => Apply();
	}
}
