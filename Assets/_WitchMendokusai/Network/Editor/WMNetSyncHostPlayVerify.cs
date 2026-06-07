using System;
using FishNet.Object;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WitchMendokusai.EditorTools;

namespace WitchMendokusai.NetworkEditor
{
	/// <summary>
	/// TASK-WM-187 — 라이브 sync 1채널 first-use 호스트 PlayMode 자율검증. 사용자 0클릭.
	///
	/// 격리 PlayMode (WM heavy-boot 와 분리 = wedge 회피, spec 정본) — 빈 씬에서 <see cref="NetworkBootstrap"/>
	/// 가 NetworkManager+Tugboat 호스트 자체연결 → 런타임 NetworkObject(<see cref="WMNetSyncSmokeBridge"/>) 스폰
	/// → 서버 SyncVar 세팅 → 클라(host client) 미러 관측 → assert OK/FAIL.
	///
	/// 다단계 흐름이라 단발-<see cref="WMPlayVerifyBase.RunVerify"/> 만으론 부족 — IsReady 안에서 state machine
	/// 진행, 결과는 RunVerify 에서 1회 로그 (base lifecycle 보존).
	/// </summary>
	[InitializeOnLoad]
	public sealed class WMNetSyncHostPlayVerify : WMPlayVerifyBase
	{
		private const int PROBE_TEST_VALUE = 4242;
		private const ushort HOST_PORT = 7771; // 게임 부팅 port 와 격리 (회귀 충돌 회피)
		private const string ISOLATED_SCENE_NAME = "_WMNetSyncSmoke";
		private const string SCREENSHOT_PATH = "Temp/wm-netsync-host-play-verify.png";

		private enum Stage
		{
			Idle,
			BootingHost,
			SpawningBridge,
			AwaitingMirror,
			Done,
		}

		private static readonly WMNetSyncHostPlayVerify Instance = new();
		static WMNetSyncHostPlayVerify() { }

		[MenuItem("WM/Verify/라이브 sync 1채널 (호스트) Play 자율검증")]
		private static void ArmFromMenu() => Instance.Arm();

		protected override string ArmPref => "WM_NETSYNC_HOST_PLAYVERIFY_ARMED";
		protected override string Tag => "[NETSYNC-HOST-187]";

		// IsReady 가 state machine 을 회전 — settle 은 짧게(미러 관측이 IsReady 안에서 다 끝남).
		protected override double SettleSeconds => 0.1;

		private Stage stage = Stage.Idle;
		private WMNetSyncSmokeBridge serverBridge;
		private NetworkObject serverNetworkObject;
		private bool hostBooted;
		private bool spawnDone;
		private int observedMirror = WMNetSyncSmokeBridge.UNINITIALIZED_VALUE;

		// EnterPlaymode 가 게임 heavy-boot 씬을 그대로 안 끌고 가도록 — 빈 씬으로 갈아끼움.
		// 현 씬이 dirty 면 자율 진행 X (사용자 작업 보호) — 명시 로그 후 abort.
		public override void Arm()
		{
			if (EditorApplication.isPlayingOrWillChangePlaymode == true)
			{
				Debug.LogWarning(Tag + " 이미 Play 상태 — Arm 무시");
				return;
			}

			Scene activeScene = EditorSceneManager.GetActiveScene();
			if (activeScene.isDirty == true)
			{
				Debug.LogError(Tag + " 활성 씬 dirty — 저장 후 다시 시도. (자율검증이 미저장 작업을 덮어쓰지 않음)");
				return;
			}

			ResetState();
			Scene isolated = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
			isolated.name = ISOLATED_SCENE_NAME;
			Debug.Log(Tag + " 격리 빈 씬 생성 (" + ISOLATED_SCENE_NAME + ") — heavy-boot wedge 회피");

			base.Arm();
		}

		// 매 tick 호출 — stage 별 1회씩 진행, 모두 완료되면 true 반환.
		protected override bool IsReady()
		{
			switch (stage)
			{
				case Stage.Idle:
					BootHost();
					return false;

				case Stage.BootingHost:
					if (NetworkBootstrap.IsHostFullyStarted == false)
						return false;
					hostBooted = true;
					stage = Stage.SpawningBridge;
					return false;

				case Stage.SpawningBridge:
					SpawnBridgeAndPushValue();
					return false;

				case Stage.AwaitingMirror:
					if (TryReadHostClientMirror(out int mirroredValue) == false)
						return false;
					observedMirror = mirroredValue;
					stage = Stage.Done;
					return true;

				case Stage.Done:
					return true;
			}
			return false;
		}

		protected override void RunVerify()
		{
			bool mirrorMatches = observedMirror == PROBE_TEST_VALUE;
			bool loopOk = hostBooted && spawnDone && mirrorMatches;

			Log((loopOk ? "LOOP OK ✅" : "LOOP FAIL ❌")
				+ " hostBooted=" + hostBooted
				+ " spawnDone=" + spawnDone
				+ " probe=set:" + PROBE_TEST_VALUE + "/mirror:" + observedMirror
				+ " match=" + mirrorMatches);

			ScreenCapture.CaptureScreenshot(SCREENSHOT_PATH);
			Log("screenshot → " + SCREENSHOT_PATH);

			// 호스트 정리 — 다음 verify run/Play 진입을 깨끗하게.
			try
			{
				NetworkBootstrap.StopHost();
			}
			catch (Exception exception)
			{
				Debug.LogWarning(Tag + " StopHost 예외(무시): " + exception.GetType().Name + " " + exception.Message);
			}
		}

		private void BootHost()
		{
			try
			{
				NetworkBootstrap.EnsureHostStarted(HOST_PORT);
				stage = Stage.BootingHost;
				Log("호스트 기동 요청 (port=" + HOST_PORT + ") — IsHostFullyStarted 대기");
			}
			catch (Exception exception)
			{
				Debug.LogError(Tag + " 호스트 기동 실패: " + exception.GetType().Name + " " + exception.Message);
				stage = Stage.Done;
			}
		}

		private void SpawnBridgeAndPushValue()
		{
			GameObject go = new GameObject("WMNetSyncSmokeBridge_Server");
			serverNetworkObject = go.AddComponent<NetworkObject>();
			serverBridge = go.AddComponent<WMNetSyncSmokeBridge>();

			try
			{
				NetworkBootstrap.ServerSpawn(serverNetworkObject);
				serverBridge.ServerSetProbe(PROBE_TEST_VALUE);
				spawnDone = true;
				stage = Stage.AwaitingMirror;
				Log("server spawn + SyncVar.set(" + PROBE_TEST_VALUE + ") — host client 미러 대기");
			}
			catch (Exception exception)
			{
				Debug.LogError(Tag + " server spawn 실패: " + exception.GetType().Name + " " + exception.Message);
				stage = Stage.Done;
			}
		}

		// host 모드라도 SyncVar 가 host client 측에 미러되면 OnChange(asServer=false) 콜백이 fire.
		// 콜백 fire = sync layer 실거동 신호 (값 메모리 공유가 아닌 큐/loopback 적용 경로 입증).
		private bool TryReadHostClientMirror(out int mirroredValue)
		{
			mirroredValue = WMNetSyncSmokeBridge.UNINITIALIZED_VALUE;
			if (serverBridge == null)
				return false;
			if (serverBridge.ClientFired == false)
				return false;

			mirroredValue = serverBridge.ClientMirroredValue;
			return true;
		}

		private void ResetState()
		{
			stage = Stage.Idle;
			serverBridge = null;
			serverNetworkObject = null;
			hostBooted = false;
			spawnDone = false;
			observedMirror = WMNetSyncSmokeBridge.UNINITIALIZED_VALUE;
		}
	}
}
