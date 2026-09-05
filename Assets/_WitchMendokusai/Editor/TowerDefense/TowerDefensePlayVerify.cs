using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
// ★ 좌표는 판정 쪽 (TASK-WM-214) — 검증 스크립트도 게임과 같은 타입으로 말해야 한다.
using Vector3 = WitchMendokusai.Numerics.Vector3;
using Vector2Int = WitchMendokusai.Numerics.Vector2Int;
using Vector3Int = WitchMendokusai.Numerics.Vector3Int;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace WitchMendokusai.EditorTools
{
	/// <summary>
	/// 특수시공 개척(TD) PlayMode 자율검증 하네스 (TASK-WM-194 증분5) — 사용자 0 클릭.
	///
	/// WMPlayVerifyBase 를 상속하지 않는다: 그 베이스는 ready 직후 RunVerify *1회 동기 실행* 모델이라
	/// "웨이브가 뜨고 → 타워가 쏘고 → 코어가 깎이는" *프레임 경과 관찰*을 못 담는다(베이스 XML 주석이
	/// 직접 예고한 예외 케이스). 그래서 동일 lifecycle 규약(arm → EnteredPlayMode → ready 게이트 →
	/// settle → HARD TIMEOUT 안전망 → 자동 ExitPlaymode)만 계승하고 본문은 관찰 루프로 구현.
	///
	/// Play 부팅 중 외부 명령 400/503 가능. 하네스의 에디터 내부 자율 구동과 Editor.log/Console 정본.
	/// 유니크 prefix [TD-Verify] 로 단일 grep.
	///
	/// ⚠ 검증 범위 — 배치는 match API 직접 호출(게임 루프 검증). 마우스 입력 경로
	/// (InputStrategyTowerDefense → TowerDefensePlacement 레이캐스트)는 본 하네스가 안 덮는다.
	/// </summary>
	[InitializeOnLoad]
	public static partial class TowerDefensePlayVerify
	{
		private const string ARM_PREF = "WM.TD.PlayVerify.Armed";
		// 결말(승/패 → 배너 → 다시 도전)만 보는 축약 모드. 전체 하네스는 배치·재시작 사이클까지 밟느라
		// 한 번에 수 분씩 에디터를 점유해, 정작 마지막에 오는 결말 검증이 하드 타임아웃에 잘려 나갔다.
		// 검증하고 싶은 구간만 빨리 도는 루프가 있어야 그 구간이 실제로 검증된다(피드백 루프 우선).
		private const string CONCLUSION_ONLY_PREF = "WM.TD.PlayVerify.ConclusionOnly";
		// 배치까지만 보는 축약 모드. 전체 실행은 방어 55초 + 무방비 판 170초까지 밟아 6분이 넘는데,
		// 「연구 인형을 지으면 배수가 오르는가」처럼 *배치 시점에 결판나는* 확인은 90초면 끝난다.
		// 확인하려는 것보다 긴 루프를 도는 것이 작업이 오래 걸린 두 번째 원인(사용자 지적).
		private const string PLACE_ONLY_PREF = "WM.TD.PlayVerify.PlaceOnly";
		private const string WAVES_ONLY_PREF = "WM.TD.PlayVerify.WavesOnly";
		private const string TAG = "[TD-Verify]";
		// 검사 전용 고정 판 — 두 실행을 견주려면 같은 땅이어야 한다. 사람이 노는 판은 그대로 매번 새로 생성된다.
		private const int VERIFY_MAP_SEED = 194194;
		private const double SETTLE_SECONDS = 2.0;
		private const double HARD_TIMEOUT = 420.0;
		private const double OBSERVE_SECONDS = 170.0; // 무방비 판 기준 — 첫 웨이브가 코어를 깎아 결말이 온다.
		private const double SAMPLE_INTERVAL = 1.0;
		// WaitWorld 가 왜 안 풀리는지(부팅 지연 vs 컨트롤러 미생성)를 구분하려면 게이트별 관측이 필요.
		private const double GATE_LOG_INTERVAL = 5.0;

		private enum Step
		{
			WaitWorld = 0,
			Settle = 1,
			EnterMode = 2,
			WaitMatch = 3,
			Place = 4,
			PlaceDump = 9,
			PlaceAfterRestartDump = 10,
			DisarmRestart = 11,
			ObserveConclusion = 12,
			VerifyConclusion = 13,
			RestartFromConclusion = 14,
			Restart = 5,
			RestartSettle = 6,
			PlaceAfterRestart = 7,
			Observe = 8,
			ObserveDefended = 15,
			SelectedLayout = 16,
			ResumeEnter = 17,
			ResumeCheck = 18,
		}

		private static Step step;
		// 선택 클릭 시각 — 패널이 열릴 한 틱을 기다리는 기준.
		private static double selectedLayoutAt;
		// 모드 이탈·재진입 시각 — 부팅이 끝날 시간을 준다.
		private static double resumeAt;
		private static bool selectedLayoutChecked;
		private static double playStart;
		private static double readyAt;
		private static double observeStart;
		private static double lastSample;
		private static double lastGateLog;
		private static bool startClicked;
		private static bool signalChecked;
		private static double markCheckAt;
		private static TowerDefenseMatch match;
		private static bool matchEndedSeen;
		private static int lastWaveIndex;
		private static TowerDefensePhase lastPhase;
		private static int lastResource;
		private static int firstContactWave;
		private static double restartAt;
		private static bool conclusionOnly;
		private static bool placeOnly;
		private static bool wavesOnly;

		static TowerDefensePlayVerify()
		{
			EditorApplication.playModeStateChanged += OnPlayModeChanged;
		}

		[MenuItem("WM/TowerDefense/Arm Play-Verify")]
		public static void Arm()
		{
			EditorPrefs.SetBool(CONCLUSION_ONLY_PREF, false);
			EditorPrefs.SetBool(PLACE_ONLY_PREF, false);
			EditorPrefs.SetBool(WAVES_ONLY_PREF, false);
			EditorPrefs.SetBool(ARM_PREF, true);
			Debug.Log(TAG + " armed — Play 진입");
			EditorApplication.EnterPlaymode();
		}

		/// <summary> 결말만 — 무방비 판으로 곧장 들어가 패배 → 배너 → 다시 도전 한 사이클만 본다. </summary>
		[MenuItem("WM/TowerDefense/Arm Play-Verify (Ending Only)")]
		public static void ArmConclusionOnly()
		{
			EditorPrefs.SetBool(PLACE_ONLY_PREF, false);
			EditorPrefs.SetBool(WAVES_ONLY_PREF, false);
			EditorPrefs.SetBool(CONCLUSION_ONLY_PREF, true);
			EditorPrefs.SetBool(ARM_PREF, true);
			Debug.Log(TAG + " armed (결말만) — Play 진입");
			EditorApplication.EnterPlaymode();
		}

		/// <summary> 배치만 — 세우는 순간 결판나는 것(비용·배수·슬롯 매핑)을 90초 안에 본다. </summary>
		[MenuItem("WM/TowerDefense/Arm Play-Verify (Placement Only)")]
		public static void ArmPlaceOnly()
		{
			EditorPrefs.SetBool(CONCLUSION_ONLY_PREF, false);
			EditorPrefs.SetBool(WAVES_ONLY_PREF, false);
			EditorPrefs.SetBool(PLACE_ONLY_PREF, true);
			EditorPrefs.SetBool(ARM_PREF, true);
			Debug.Log(TAG + " armed (배치만) — Play 진입");
			EditorApplication.EnterPlaymode();
		}

		/// <summary>
		/// 파도만 — 마수가 도는 동안만 본다. 배치·결말·이어하기·재시작을 건너뛴다.
		///
		/// ★ 왜 필요한가: 파도 중에만 나타나는 것(굳는 마수·사격 소음·성능)을 한 번 확인하려고
		///   배치→파도→결말→재시작 전 과정 5분을 매번 태웠다. 확인 하나에 5분은 너무 비싸서
		///   「한 사이클에 한 번」밖에 못 본다 — 진단이 느려지는 진짜 이유가 이것이었다.
		/// </summary>
		[MenuItem("WM/TowerDefense/Arm Play-Verify (Waves Only)")]
		public static void ArmWavesOnly()
		{
			EditorPrefs.SetBool(CONCLUSION_ONLY_PREF, false);
			EditorPrefs.SetBool(PLACE_ONLY_PREF, false);
			EditorPrefs.SetBool(WAVES_ONLY_PREF, true);
			EditorPrefs.SetBool(ARM_PREF, true);
			Debug.Log(TAG + " armed (파도만) — Play 진입");
			EditorApplication.EnterPlaymode();
		}

		private static void OnPlayModeChanged(PlayModeStateChange change)
		{
			if (change != PlayModeStateChange.EnteredPlayMode || EditorPrefs.GetBool(ARM_PREF, false) == false)
				return;

			EditorPrefs.SetBool(ARM_PREF, false);
			conclusionOnly = EditorPrefs.GetBool(CONCLUSION_ONLY_PREF, false);
			placeOnly = EditorPrefs.GetBool(PLACE_ONLY_PREF, false);
			wavesOnly = EditorPrefs.GetBool(WAVES_ONLY_PREF, false);
			step = Step.WaitWorld;
			playStart = EditorApplication.timeSinceStartup;
			readyAt = -1.0;
			observeStart = -1.0;
			lastSample = -1.0;
			lastGateLog = -1.0;
			startClicked = false;
			signalChecked = false;
			adaptationProbeAt = 0.0;
			adaptationArmed = false;
			adaptationSawEnemies = false;
			adaptationTargetsNest = false;
			adaptationMatch = null;
			breachCheckAt = 0.0;
			noiseArmed = false;
			noiseCheckAt = 0.0;
			noiseMatch = null;
			noiseAlertSeen = false;
			noiseSustainAt = 0.0;
			noiseRampLeft = 0;
			resumeVerified = false;
			noiseWarnSeenAt = 0.0;
			breachArmed = false;
			breachMatch = null;
			markCheckAt = 0.0;
			lairDriftCheckAt = 0.0;
			pressureCheckAt = 0.0;
			lairClearCheckAt = 0.0;
			selectedLayoutChecked = false; // 판마다 다시 잰다.
			assaultStart = -1.0;
			lastAliveEnemyCount = -1;
			stuckDumped = false;
			heroCommanded = false;
			heroProbeReady = false;
			dollsReported = false;
			lastPerfLog = 0.0;
			frameSamples = 0;
			frameTimeSum = 0f;
			perfPeakAlive = 0;
			match = null;
			matchEndedSeen = false;
			lastWaveIndex = -1;
			lastPhase = TowerDefensePhase.Concluded;
			lastResource = -1;
			firstContactWave = -1;
			defendedStart = -1.0;
			defendedLastResource = -1;
			killIncomeEvents = 0;
			lastDumpedWave = -1;
			waveDumpAt = -1.0;
			towerPlanFlipped = false;
			firstWaveCalled = false;
			EditorApplication.update += Tick;
			Debug.Log(TAG + " EnteredPlayMode — World ready 대기");
		}

		/// <summary>
		/// 배치 검증 — **마우스 경로 그대로**: 목표 월드좌표를 모드 카메라로 화면좌표 환산 후
		/// TowerDefensePlacement.PlaceXAt(화면좌표) 호출. 즉 카메라 설정·지면 레이캐스트·셀 스냅·
		/// 노드 반경 판정까지 실제 클릭과 동일 경로를 탄다(버그가 숨는 자리가 바로 여기).
		/// 유일한 미포함 = 물리 마우스 버튼 이벤트 → InputStrategy 콜백 디스패치(얇은 글루).
		/// </summary>
		/// <summary>
		/// 코어 주변에서 배치 가능한(암반 아님·비어 있음) 칸을 count 개 찾는다 — 생성된 판마다 자리가 다르므로
		/// 하네스가 좌표를 박아두면 "배치했다고 믿는 무방비 판"이 된다.
		/// </summary>
		private static List<Vector3> FindPlaceableSpots(Transform stageRoot, int count)
		{
			List<Vector3> spots = new();
			if (match == null || stageRoot == null)
				return spots;

			// 코어에서 바깥으로 링을 넓혀가며 훑는다(코어 근처 = 방어선으로 말이 되는 자리).
			for (int radius = 2; radius <= 10 && spots.Count < count; radius++)
			{
				for (int angleStep = 0; angleStep < 16 && spots.Count < count; angleStep++)
				{
					float angle = angleStep * Mathf.PI * 2f / 16f;
					Vector3 local = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
					Vector3 snapped = new Vector3(Mathf.Floor(local.x) + 0.5f, 0f, Mathf.Floor(local.z) + 0.5f);
					if (spots.Contains(snapped))
						continue;
					if (match.IsCellOccupied(stageRoot.TransformPoint(snapped.ToUnity()).ToSim()))
						continue;
					spots.Add(snapped);
				}
			}
			return spots;
		}

		/// <summary>
		/// 영웅(움직이는 내 편) + 이름표(인형이 아이가 됐나). 둘 다 「있다」가 아니라 *실제로 움직이는가 /
		/// 실제로 붙었는가*를 본다 — 스테이지에 영웅이 미설정이면 그 사실을 SKIP 으로 남긴다(거짓 실패 금지).
		/// </summary>
		private static void VerifyHeroAndNames(Transform stageRoot)
		{
			// 영웅 명령은 여기서 안 한다 — 스폰이 코루틴이라 이 시점엔 아직 없을 수 있다(라이브 실측: HERO-SKIP).
			// 「있으면 명령한다」를 관찰 루프가 맡는다(있는지 없는지를 시점 하나로 단정하지 않는다).
			// 이름표도 마찬가지 — 배치 스폰이 코루틴이라 *이 틱엔 아직* 붙지 않았다. 관찰 루프가 확인한다.
		}

		// 영웅이 명령한 쪽으로 *실제로 걸어갔는지*는 다음 관찰 틱에서 본다(같은 프레임엔 아직 안 움직였다).
		private static Vector3 heroProbeFrom;
		private static Vector3 heroProbeTarget;
		private static bool heroProbeReady;
		private static bool heroCommanded;
		private static bool dollsReported;

		// 성능 실측 — 프레임 시간 평균과 최다 마릿수.
		private const double PERF_LOG_INTERVAL = 3.0;
		private static double lastPerfLog;
		private static int frameSamples;
		private static float frameTimeSum;
		private static int perfPeakAlive;
		private static double heroProbeAt;

		// HUD 실재 확인 — 화면에 숫자가 안 뜨면 사람이 플레이 판단을 못 한다(이번 증분의 핵심 산출).
		/// <summary>
		/// 신호장이 *실제로* 서는가 — 코어가 차고, 덮는 원이 자라고, 그림(테두리·파동)이 씬에 있는가.
		///
		/// ★ 이걸 안 재면 「컴파일은 초록인데 판에서는 아무것도 안 켜지는」 상태를 못 잡는다 —
		///   실제로 그 상태를 한 번 겪었다(충전값을 좌표로 짝지어 매 프레임 0 으로 되돌아갔다).
		/// </summary>
		private static void VerifySignalField()
		{
			if (match == null)
				return;

			float charge = match.CoreSignalCharge;
			float radius = match.CoreSignalRadius;
			int nodes = match.SignalNodeCount;

			int edges = 0;
			int pulses = 0;
			foreach (GameObject candidate in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
			{
				if (candidate.name == "SignalEdge")
					edges++;
				else if (candidate.name == "SignalPulse")
					pulses++;
			}

			// ★ 「왜 0 인가」를 가르는 값들을 같이 찍는다 — 판이 아직 안 도는 것과 계산이 안 도는 것은
			//   똑같이 0 으로 보이는데 고치는 자리가 전혀 다르다.
			// ★ 중계가 하나도 없으면 이 검사는 영영 「0」이다 — 그런 검사는 있으나 마나다.
			//   코어 옆에 하나 **일부러 세워서** 「받는가 · 용량이 느는가」를 실제로 재게 만든다.
			if (match.FedRelayCount == 0 && match.CoreCombatant != null && relayProbeAt <= 0.0)
			{
				Vector3 beside = match.CoreCombatant.Position + new Vector3(4f, 0f, 4f);
				bool placed = match.TryPlaceGenerator(beside);
				relayCapacityBefore = match.PowerCapacity;
				relayProbeAt = EditorApplication.timeSinceStartup + 6.0;
				Debug.Log($"{TAG} 신호 사슬 — 코어 옆에 발전 인형 세우기 {placed} (용량 {relayCapacityBefore})");
			}

			// ★ 컨트롤넷의 핵심 약속 = 「중계탑이 신호를 *받아서* 넘긴다」. 받는 중계가 0 이면
			//   사슬이 한 칸도 안 뻗은 것이고, 전기는 결국 「코어 반경 안」이 전부가 된다.
			Debug.Log($"{TAG} 신호 사슬 — 신호 받는 중계 {match.FedRelayCount}기 / 노드 {nodes - 1}기"
				+ $" · 용량 {match.PowerCapacity}(코어만이면 6)");

			Debug.Log($"{TAG} 신호장 — 코어 충전 {charge:F2} 덮는반경 {radius:F1} 노드 {nodes}"
				+ $" / 그림 테두리 {edges} 파동 {pulses}"
				+ $" / 전기 용량 {match.PowerCapacity} 요구 {match.PowerDemand} 버틴시간 {match.SurvivedSeconds}s");

			if (charge <= 0f)
				Debug.LogError($"{TAG} 신호장 FAIL — 코어가 안 찬다(충전 0). 이 상태면 판의 모든 건물이 멈춘다.");
			if (radius <= 0f)
				Debug.LogError($"{TAG} 신호장 FAIL — 덮는 반경 0. 전기를 받는 건물이 하나도 없다.");
			if (edges == 0)
				Debug.LogError($"{TAG} 신호장 FAIL — 테두리 원이 씬에 0개. 화면에 아무것도 안 보인다는 뜻.");
		}

		/// <summary> 지금 스테이지의 목줄 반경 — 판정 기준을 규칙에서 그대로 읽는다(하네스가 따로 박지 않는다). </summary>
		private static float TowerDefenseModeControllerLeash()
		{
			TowerDefenseModeController controller = Object.FindAnyObjectByType<TowerDefenseModeController>();
			return controller != null && controller.Stage != null ? controller.Stage.LairLeashRadius : 0f;
		}

		private static Transform FindStageRoot()
		{
			if (TowerDefenseModeController.TryGetExistingInstance(out TowerDefenseModeController controller) == false)
				return null;
			return controller.transform.Find("StageRoot");
		}

		// 스테이지 주변 살아있는 적(공격팀) 수 — 매치 내부 상태를 안 뚫고 라이브 씬에서 센다.
		private static int CountEnemiesNear(Transform stageRoot)
		{
			if (stageRoot == null)
				return 0;

			int count = 0;
			MatchCombatant[] combatants = Object.FindObjectsByType<MatchCombatant>(FindObjectsInactive.Exclude);
			foreach (MatchCombatant combatant in combatants)
			{
				if (combatant.TeamId != 1 || combatant.IsAlive == false)
					continue;
				if ((combatant.Position - stageRoot.position.ToSim()).sqrMagnitude < 100f * 100f)
					count++;
			}
			return count;
		}

		private static bool SceneIsWorld()
		{
			Scene active = SceneManager.GetActiveScene();
			return active.IsValid() && active.name == "World" && active.isLoaded;
		}
	}
}
