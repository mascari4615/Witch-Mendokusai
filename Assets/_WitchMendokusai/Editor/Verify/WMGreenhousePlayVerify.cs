using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using WitchMendokusai;

namespace WitchMendokusai.EditorTools
{
	// 마도 온실(TASK-WM-167) PlayMode 자율 behavior-verify — 사용자 0클릭.
	// WM Play 는 MCP 브리지를 wedge 시키므로(canon: run_tests(PlayMode) wedge) MCP 로 Play 중 구동 불가 →
	// 이 하네스가 *에디터 안에서* 스스로: Play 진입 → World 씬 준비 대기 → WitchGreenhouseObject 스폰(Start 자립
	// 구축) → demoTick 몇 초 → 칸 상태 로그(유니크 prefix) → 스크린샷 → 자동 ExitPlaymode. Editor.log 가 ground-truth.
	// [[wm-playmode-autoverify-bootready-gate]] 패턴. 하드 타임아웃 = 절대 Play 에 안 물리게(공유 에디터 보호).
	[InitializeOnLoad]
	public static class WMGreenhousePlayVerify
	{
		private const string ARM_PREF = "WM_GH_PLAYVERIFY_ARMED";
		private const string TAG = "[GH-PLAY-9d4]";
		private const double SETTLE_SECONDS = 6.0;   // World 준비 후 demoTick 관찰 시간
		private const double HARD_TIMEOUT = 40.0;     // 이 시간 넘으면 무조건 Play 탈출(안전망)

		private static double playStart;
		private static double spawnAt = -1.0;
		private static bool spawned;
		private static WitchGreenhouseObject house;

		static WMGreenhousePlayVerify()
		{
			EditorApplication.playModeStateChanged += OnPlayModeChanged;
		}

		[MenuItem("WM/Verify/마도온실 Play 자율검증")]
		public static void Arm()
		{
			EditorPrefs.SetBool(ARM_PREF, true);
			Debug.Log(TAG + " armed — Play 진입");
			EditorApplication.EnterPlaymode();
		}

		private static void OnPlayModeChanged(PlayModeStateChange change)
		{
			if (change == PlayModeStateChange.EnteredPlayMode && EditorPrefs.GetBool(ARM_PREF, false))
			{
				EditorPrefs.SetBool(ARM_PREF, false);
				playStart = EditorApplication.timeSinceStartup;
				spawnAt = -1.0;
				spawned = false;
				house = null;
				EditorApplication.update += Tick;
				Debug.Log(TAG + " EnteredPlayMode — World 대기 시작");
			}
		}

		private static void Tick()
		{
			double now = EditorApplication.timeSinceStartup;

			// 안전망: 무슨 일이 있어도 HARD_TIMEOUT 넘으면 Play 탈출(공유 에디터 보호).
			if (now - playStart > HARD_TIMEOUT)
			{
				Debug.LogError(TAG + " TIMEOUT — World 미준비 또는 행. Play 강제 종료.");
				Finish();
				return;
			}

			// World 씬 준비 전엔 대기.
			if (spawned == false)
			{
				Scene active = SceneManager.GetActiveScene();
				if (active.IsValid() == false || active.name != "World" || active.isLoaded == false)
				{
					return;
				}

				// World 준비 — 온실 스폰(Start 가 자립 구축+placeholder+demoTick 시작).
				GameObject go = new("[Verify] 마도 온실");
				house = go.AddComponent<WitchGreenhouseObject>();
				spawned = true;
				spawnAt = now;
				Debug.Log(TAG + " World 준비 — 온실 스폰됨. demoTick 관찰 " + SETTLE_SECONDS + "s");
				return;
			}

			// demoTick 관찰 후 결과 로그 + 종료.
			if (now - spawnAt >= SETTLE_SECONDS)
			{
				ReportAndFinish();
			}
		}

		private static void ReportAndFinish()
		{
			if (house == null || house.Model == null)
			{
				Debug.LogError(TAG + " FAIL — house/Model null (자립 구축 안 됨)");
				Finish();
				return;
			}

			int plotCount = house.Model.PlotCount;
			int living = house.Model.LivingCount();
			System.Text.StringBuilder phases = new();
			foreach (System.Collections.Generic.KeyValuePair<int, DomainSDK.Farming.GreenhousePlot> entry in house.Model.Plots)
			{
				phases.Append(entry.Key).Append('=').Append(entry.Value.Phase).Append(' ');
			}

			bool ok = plotCount > 0;
			Debug.Log(TAG + (ok ? " SELF-BUILD OK" : " FAIL") + " plotCount=" + plotCount + " living=" + living + " phases=[ " + phases + "] specimenCount=" + house.SpecimenCount);

			string shot = "Temp/gh-play-verify.png";
			ScreenCapture.CaptureScreenshot(shot);
			Debug.Log(TAG + " screenshot → " + shot);

			Finish();
		}

		private static void Finish()
		{
			EditorApplication.update -= Tick;
			if (EditorApplication.isPlaying)
			{
				EditorApplication.ExitPlaymode();
			}
		}
	}
}
