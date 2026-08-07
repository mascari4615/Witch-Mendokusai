using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
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
	/// MCP 는 Play 중 wedge 되므로 하네스가 *에디터 안에서* 스스로 구동하고 Editor.log/Console 이 ground-truth.
	/// 유니크 prefix [TD-Verify] 로 단일 grep.
	///
	/// ⚠ 검증 범위 — 배치는 match API 직접 호출(게임 루프 검증). 마우스 입력 경로
	/// (InputStrategyTowerDefense → TowerDefensePlacement 레이캐스트)는 본 하네스가 안 덮는다.
	/// </summary>
	[InitializeOnLoad]
	public static class TowerDefensePlayVerify
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
		private const string TAG = "[TD-Verify]";
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
		private static double defendedStart;
		private static int defendedLastResource;
		private static int killIncomeEvents;
		private static bool towerPlanFlipped;
		// 판매 검증용 — 배치 좌표. 스폰은 코루틴(1프레임 양보)이라 *같은 틱에 팔면 아직 아무도 없다*.
		private static Vector3 sellProbeLocal;
		private static int sellProbeSlot;
		private static bool sellProbeReady;
		private static readonly List<int> nodeOrder = new();
		private static bool firstWaveCalled;
		private static int lastDumpedWave;
		private static double waveDumpAt;

		static TowerDefensePlayVerify()
		{
			EditorApplication.playModeStateChanged += OnPlayModeChanged;
		}

		[MenuItem("WM/TowerDefense/Arm Play-Verify")]
		public static void Arm()
		{
			EditorPrefs.SetBool(CONCLUSION_ONLY_PREF, false);
			EditorPrefs.SetBool(PLACE_ONLY_PREF, false);
			EditorPrefs.SetBool(ARM_PREF, true);
			Debug.Log(TAG + " armed — Play 진입");
			EditorApplication.EnterPlaymode();
		}

		/// <summary> 결말만 — 무방비 판으로 곧장 들어가 패배 → 배너 → 다시 도전 한 사이클만 본다. </summary>
		[MenuItem("WM/TowerDefense/Arm Play-Verify (결말만)")]
		public static void ArmConclusionOnly()
		{
			EditorPrefs.SetBool(PLACE_ONLY_PREF, false);
			EditorPrefs.SetBool(CONCLUSION_ONLY_PREF, true);
			EditorPrefs.SetBool(ARM_PREF, true);
			Debug.Log(TAG + " armed (결말만) — Play 진입");
			EditorApplication.EnterPlaymode();
		}

		/// <summary> 배치만 — 세우는 순간 결판나는 것(비용·배수·슬롯 매핑)을 90초 안에 본다. </summary>
		[MenuItem("WM/TowerDefense/Arm Play-Verify (배치만)")]
		public static void ArmPlaceOnly()
		{
			EditorPrefs.SetBool(CONCLUSION_ONLY_PREF, false);
			EditorPrefs.SetBool(PLACE_ONLY_PREF, true);
			EditorPrefs.SetBool(ARM_PREF, true);
			Debug.Log(TAG + " armed (배치만) — Play 진입");
			EditorApplication.EnterPlaymode();
		}

		private static void OnPlayModeChanged(PlayModeStateChange change)
		{
			if (change != PlayModeStateChange.EnteredPlayMode || EditorPrefs.GetBool(ARM_PREF, false) == false)
				return;

			EditorPrefs.SetBool(ARM_PREF, false);
			conclusionOnly = EditorPrefs.GetBool(CONCLUSION_ONLY_PREF, false);
			placeOnly = EditorPrefs.GetBool(PLACE_ONLY_PREF, false);
			step = Step.WaitWorld;
			playStart = EditorApplication.timeSinceStartup;
			readyAt = -1.0;
			observeStart = -1.0;
			lastSample = -1.0;
			lastGateLog = -1.0;
			startClicked = false;
			signalChecked = false;
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

		private static void Tick()
		{
			double now = EditorApplication.timeSinceStartup;

			// 안전망 — 무슨 일이 있어도 공유 에디터를 Play 에 물리지 않는다.
			if (now - playStart > HARD_TIMEOUT)
			{
				// 타임아웃이 그냥 "행"으로만 끝나면 몇 분짜리 실행이 통째로 버려진다 — 죽기 전에 아는 것을 전부 말한다.
				Debug.LogError(TAG + " TIMEOUT — 단계=" + step + " 에서 행. Play 강제 종료."
					+ " match=" + (match != null)
					+ (match != null
						? " phase=" + match.Phase + " wave=" + match.WaveIndex + " outcome=" + match.Outcome
							+ " resource=" + match.Resource
							+ " coreAlive=" + (match.CoreCombatant != null && match.CoreCombatant.IsAlive)
						: string.Empty)
					+ " endedEvent=" + matchEndedSeen
					+ " observed=" + (observeStart > 0 ? (now - observeStart).ToString("F1") : "n/a"));
				Finish();
				return;
			}

			if (lairClearCheckAt > 0.0 && now >= lairClearCheckAt)
			{
				lairClearCheckAt = 0.0;
				CheckLairClearReward();
			}

			if (lairDriftCheckAt > 0.0 && now >= lairDriftCheckAt)
			{
				lairDriftCheckAt = 0.0;
				CheckLairDrift();
			}

			if (pressureCheckAt > 0.0 && now >= pressureCheckAt)
			{
				pressureCheckAt = 0.0;
				CheckPressureNotice();
			}

			if (markCheckAt > 0.0 && now >= markCheckAt)
			{
				markCheckAt = 0.0;
				CountOnScreenMarks();
			}

			// ★ 신호·서식지는 **판이 도는 중에** 재야 한다. 배치 직후는 아직 첫 틱도 안 돈 시점이라
			//   전기 계산이 시작조차 안 했고, 서식지 스폰 코루틴도 절반만 끝나 있다 — 거기서 재면
			//   멀쩡한 것을 「0 이다」라고 잡는다(실측으로 한 번 겪었다: 버틴시간 0초에 전부 0).
			if (signalChecked == false && match != null && match.SurvivedSeconds >= 5)
			{
				signalChecked = true;
				VerifySignalField();
				VerifyLairsAndInvasion();
				VerifyOnScreenMarks();
			}

			switch (step)
			{
				case Step.WaitWorld:
					{
						bool sceneOk = SceneIsWorld();
						bool bootOk = BootObserver.ReachedWorld;
						bool modeOk = GameModeManager.TryGetExistingInstance(out GameModeManager _);
						bool ctrlOk = TowerDefenseModeController.TryGetExistingInstance(out TowerDefenseModeController controller);

						// 게이트별 주기 로그 — "행"이 부팅 지연인지 컨트롤러 미생성인지 로그만으로 갈린다.
						if (now - lastGateLog >= GATE_LOG_INTERVAL)
						{
							lastGateLog = now;
							Debug.Log(TAG + " GATE t=" + (now - playStart).ToString("F0")
								+ " scene=" + SceneManager.GetActiveScene().name
								+ " sceneIsWorld=" + sceneOk
								+ " bootPhase=" + BootObserver.Current
								+ " gameModeMgr=" + modeOk
								+ " tdController=" + ctrlOk);
						}

						// 로비 자동 통과 — AppSettings.AutoStart 기본값이 false 라(헤드리스/dev 옵션에서만 true)
						// 부팅이 DataReady 에서 사람 클릭을 기다린다. 사용자의 저장 설정을 건드리지 않고
						// 하네스가 "시작" 을 대신 눌러 World 로 넘긴다(멱등 — startClicked 1회).
						if (bootOk == false && startClicked == false
							&& BootObserver.Current == BootPhase.DataReady
							&& LobbyManager.Instance != null)
						{
							startClicked = true;
							Debug.Log(TAG + " LOBBY-AUTOSTART — AutoStart=false 라 하네스가 StartGame 대행");
							LobbyManager.Instance.StartGame();
							return;
						}

						if (sceneOk == false || bootOk == false || modeOk == false || ctrlOk == false)
							return;

						match = controller.GetComponent<TowerDefenseMatch>();
						Debug.Log(TAG + " BOOT-OK controller=True match=" + (match != null));
						readyAt = now;
						step = Step.Settle;
						return;
					}

				case Step.Settle:
					if (now - readyAt < SETTLE_SECONDS)
						return;
					step = Step.EnterMode;
					return;

				case Step.EnterMode:
					if (GameModeManager.TryGetExistingInstance(out GameModeManager modeManager) == false)
						return;
					VerifyTimetoHub();
					modeManager.SetMode(GameMode.TowerDefense);
					match.MatchEnded += OnMatchEnded;
					Debug.Log(TAG + " ENTER-MODE mode=" + modeManager.CurrentMode);
					DumpCameras("진입 직후");
					step = Step.WaitMatch;
					return;

				case Step.WaitMatch:
					// 코어 생성 = TowerDefenseCore 존재 = Resource 가 시작자원으로 채워짐.
					if (match == null || match.Resource <= 0)
						return;
					Debug.Log(TAG + " MATCH-READY resource=" + match.Resource + " phase=" + match.Phase
						+ " conclusionOnly=" + conclusionOnly);
					// 결말만 모드 = 아무것도 짓지 않은 채 그대로 관측 — 이미 무방비 상태라 재시작조차 필요 없다.
					if (conclusionOnly)
					{
						restartAt = now;
						step = Step.ObserveConclusion;
						return;
					}
					step = Step.Place;
					return;

				case Step.Place:
					DoPlacements();
					// 2차 덤프 — "진입 시엔 켜졌는데 이후 덮인다"를 잡으려면 시간 경과 후 한 번 더 봐야 한다.
					DumpCameras("배치 후");
					restartAt = now;
					step = Step.PlaceDump;
					return;

				// 배치 스폰은 코루틴(1프레임 양보 후 Init/등록)이라 **같은 틱에 덤프하면 아무것도 안 보인다**
				// (첫 시도에서 "수비 유닛 1기"만 찍혀 진단이 통째 유실됐다). 등록이 끝날 시간을 준다.
				case Step.PlaceDump:
					if (now - restartAt < 1.5)
						return;
					DumpPlacedUnits("최초 배치");
					VerifyUiPointerGuard();
					VerifyHudLayout("평상시");
					// ★ 평상시엔 안 뜨는 것(선택 패널)이 *뜬 상태*로도 재야 한다 — 안 뜬 것은 겹칠 수도 없어서
					//   「겹침 0」이 「띄워본 적이 없다」를 숨긴다.
					// ★ 코어 카드는 레벨이 올라야 뜬다 — 안 띄우면 「카드가 겹치나」를 영영 못 잰다.
					if (match != null)
						match.GrantCoreExperienceForVerification(CORE_XP_FOR_CARDS);
					VerifyBuildingPerk();
					SelectPlacedBuildingForLayout();
					// ★ 예산은 유한하고 확인할 항목은 여럿이다 — 순서가 곧 검증 가능 여부다.
					//   승급은 *이미 서 있는 포탑*이 필요하므로 판매보다 먼저, 비싼 연구 인형은 맨 뒤.
					// 예산 순 — 채집(60)이 필요한 정수 확인을 먼저, 비싼 연구를 맨 뒤로.
					VerifyEssence();
					VerifyUpgrade();
					VerifyEssenceShortageTalks();
					VerifySell();
					VerifyTrap();
					VerifyWall();
					VerifyResearch();
					VerifyWaveEvents();
					VerifySupply();
					// 씨앗 공유는 새 판에서만 확인된다 — 재시작이 그 새 판이므로 여기서 걸어둔다.
					ArmSeedShareCheck();
					// ★ 전체 실행도 이 길로 보낸다. 예전엔 「배치만」 변형만 들렀는데, 그 변형을 아무도
					//   안 돌리는 바람에 *선택 패널 겹침 · 툴팁 겹침 · 코어 카드 · 이어하기* 검사가
					//   로그 전체에서 0회였다(오늘 하루치를 다 뒤져도 한 줄도 없다). 도는 길에 있어야 검사다.
					step = Step.SelectedLayout;
					selectedLayoutAt = now;
					return;

				// ★ 패널은 *다음 배치 패스*에 열린다 — 클릭한 그 틱에 재면 「아직 안 뜬 것」을 재고
				//   「겹침 0」이라 적는다(거짓 통과). 한 틱 기다렸다 잰다.
				case Step.SelectedLayout:
					// ★ 서식지 이동 측정이 아직 안 끝났으면 모드를 나가지 않는다 — 나가면 판이 새로 태어나
					//   깨운 서식지가 통째로 사라져 그 측정이 영영 성립하지 않는다.
					if (lairDriftCheckAt > 0.0 || lairClearCheckAt > 0.0 || pressureCheckAt > 0.0)
						return;

					// ★ 앞 단계에서 열어둔 성좌를 *여기서* 닫는다. 닫는 자리를 재시작 단계에 뒀더니,
					//   이 단계가 그 앞으로 끼어드는 순간 성좌가 열린 채로 남아 판이 멈추고,
					//   멈춘 판은 시계가 안 가니 아래 시계 게이트를 영영 못 넘어 7분 뒤 시간초과로 죽었다.
					//   덮는 것을 여는 쪽과 닫는 쪽이 다른 단계에 있으면 이런 교착이 생긴다.
					MeasureAndCloseResearchPanel();
					if (now - selectedLayoutAt < 0.3)
						return;
					// ★ 겹침은 한 번만 잰다 — 아래 시계 게이트가 이 단계를 여러 틱 돌리므로,
					//   안 막으면 같은 판정이 로그를 도배해 진짜 신호가 묻힌다.
					if (selectedLayoutChecked == false)
					{
						selectedLayoutChecked = true;
						VerifyHudLayout("건물 선택 중", mustBeUp: "SelectionPanel");
					ShowTooltipForLayout();
					VerifyHudLayout("툴팁 떠 있음");
					SelectCoreForLayout();
					VerifyCoreCards();
					}
					// ★ 시계가 0 일 때 나가면 「되감겼는지」를 가릴 수 없다(0 이나 1 이나 통과).
					//   눈금이 실제로 쌓인 뒤에 나가야 이어하기가 시계를 지키는지가 증명된다.
					if (match != null && match.SurvivedSeconds < RESUME_MIN_CLOCK)
						return;
					CaptureResumeSnapshot();
					if (GameModeManager.TryGetExistingInstance(out GameModeManager exitManager))
						exitManager.SetMode(GameMode.Default);
					resumeAt = now;
					step = Step.ResumeEnter;
					return;

				// ★ 「잠깐 접어둔다」가 진짜인지 — 나갔다 들어와서 그 판이 그대로 있는지 본다.
				//   이게 없으면 저장은 *써지기만 하고 아무도 안 읽는* 상태로도 통과한다(실제로 그랬다).
				case Step.ResumeEnter:
					if (now - resumeAt < 1.5)
						return;
					if (GameModeManager.TryGetExistingInstance(out GameModeManager enterManager))
						enterManager.SetMode(GameMode.TowerDefense);
					resumeAt = now;
					step = Step.ResumeCheck;
					return;

				case Step.ResumeCheck:
					if (now - resumeAt < 4.0)
						return;
					// ★ 복원이 끝날 때까지 이 단계에 머문다 — 도는 중에 재면 중간값을 결함으로 잡는다.
					{
						TowerDefenseMatch restoring = Object.FindAnyObjectByType<TowerDefenseMatch>();
						if (restoring != null && restoring.RestoreInProgress)
							return;
					}
					VerifyResume();
					if (placeOnly)
					{
						// 아직 재기로 한 것이 남아 있으면 끝내지 않는다 — 끝내버리면 그 항목은 영영 안 재진다.
						if (lairDriftCheckAt > 0.0 || markCheckAt > 0.0 || lairClearCheckAt > 0.0 || pressureCheckAt > 0.0)
							return;

						Debug.Log(TAG + " PLACE-ONLY 배치 확인 끝 — 조기 종료");
						Finish();
						return;
					}

					// 전체 실행은 이어하기까지 본 뒤 원래 가던 재시작으로 잇는다.
					restartAt = now;
					step = Step.Restart;
					return;

				case Step.PlaceAfterRestartDump:
					if (now - restartAt < 1.5)
						return;
					DumpPlacedUnits("재시작 후 배치");
					// ★ 씨앗 공유는 *새 판*에서만 확인된다 — 이어하기는 저장이 씨앗을 정하므로.
					VerifySeedShare();
					defendedStart = now;
					defendedLastResource = match != null ? match.Resource : -1;
					killIncomeEvents = 0;
					step = Step.ObserveDefended;
					return;

				// ★ 방어를 세운 채 한 판을 실제로 지켜본다. 이 구간이 없으면 「마수를 잡았을 때 무슨 일이
				//   일어나는가」가 통째로 미검증으로 남는다(무방비 판은 아무도 안 죽으니 격파 보상이 안 보인다).
				case Step.ObserveDefended:
					ObserveDefended(now);
					return;

				// ★ 결말(패배)을 *빠르고 확실하게* 관측하기 위한 무방비 판.
				//   방어를 세워두면 여러 웨이브를 버텨 결말까지 수 분이 걸리고, 그동안 "게임이 끝나는가"는
				//   영영 미검증으로 남는다(실제로 그랬다). 아무것도 안 지으면 첫 웨이브가 코어를 깎아
				//   결말이 결정적으로 온다 — 조작이 아니라 *실제 게임 규칙 그대로*의 최단 경로다.
				// ★ 정수는 *늦게* 붙는다 — 발전 인형도 코루틴으로 서고 전기·보급 셈은 그 다음이라,
				//   배치 직후에 재면 늘 「전기가 안 닿음」이다(실측: 그때는 0, 1분 뒤에 물으니 1이었다).
				//   기다리는 시간을 늘려 맞추려다 순서만 깨뜨렸다 — 방어 관찰이 끝난 *여기서* 한 번 더 잰다.
				case Step.DisarmRestart:
					VerifyEssence("늦게");
					if (TowerDefenseModeController.TryGetExistingInstance(out TowerDefenseModeController disarmController) == false)
					{
						Debug.LogError(TAG + " DISARM-FAIL controller 없음");
						Finish();
						return;
					}
					Debug.Log(TAG + " DISARM-RESTART 무방비 판 시작 — 결말 도달 관측");
					disarmController.Restart();
					restartAt = now;
					step = Step.ObserveConclusion;
					return;

				case Step.ObserveConclusion:
					if (now - restartAt < 3.0)
						return;
					if (observeStart < 0.0 || observeStart < restartAt)
					{
						observeStart = now;
						lastSample = now;
						matchEndedSeen = false;
						if (match != null)
							match.RequestNextWave(); // 첫 웨이브는 불러야 온다 — 무방비 판도 마찬가지.
					}
					Observe(now);
					return;

				// ★ 재시작은 풀 재사용이 처음으로 *실제로* 일어나는 지점이다 — 최초 매치는 늘 새 인스턴스라
				//   초기화 누락이 드러나지 않는다. 사용자 실증("재시작하면 초기화가 덜 되고 위치도 이상")을
				//   재현하려면 하네스가 반드시 이 사이클을 밟아야 한다.
				case Step.Restart:
					// 앞 단계에서 열어둔 성좌 — *자리가 잡힌 지금* 재고 닫는다(재시작 전에).
					MeasureAndCloseResearchPanel();
					if (TowerDefenseModeController.TryGetExistingInstance(out TowerDefenseModeController restartController) == false)
					{
						Debug.LogError(TAG + " RESTART-FAIL controller 없음");
						Finish();
						return;
					}
					Debug.Log(TAG + " RESTART 요청 — 풀 재사용 경로 진입");
					restartController.Restart();
					restartAt = now;
					step = Step.RestartSettle;
					return;

				case Step.RestartSettle:
					// 재시작은 1프레임 양보 + 코어 스폰 코루틴을 거친다. 자원이 시작값으로 돌아오면 준비 완료.
					if (now - restartAt < 2.0)
						return;
					if (match == null || match.Resource <= 0)
					{
						if (now - restartAt > 15.0)
						{
							Debug.LogError(TAG + " RESTART-FAIL 재시작 후 매치가 안 살아남 resource=" + (match != null ? match.Resource : -1));
							Finish();
						}
						return;
					}
					Debug.Log(TAG + " RESTART-READY resource=" + match.Resource + " phase=" + match.Phase + " wave=" + match.WaveIndex);
					step = Step.PlaceAfterRestart;
					return;

				case Step.PlaceAfterRestart:
					DumpPlacedUnits("재시작 직후(배치 전)"); // 코어만 있어야 하고, 코어는 반드시 (0,0,0).
					DoPlacements();
					restartAt = now;
					step = Step.PlaceAfterRestartDump;
					return;

				case Step.Observe:
					Observe(now);
					return;

				case Step.VerifyConclusion:
					VerifyConclusion(now);
					return;

				case Step.RestartFromConclusion:
					VerifyRestartFromConclusion(now);
					return;
			}
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
					if (match.IsCellOccupied(stageRoot.TransformPoint(snapped)))
						continue;
					spots.Add(snapped);
				}
			}
			return spots;
		}

		private static void DoPlacements()
		{
			Transform stageRoot = FindStageRoot();
			if (stageRoot == null)
			{
				Debug.LogError(TAG + " PLACE-FAIL StageRoot 없음");
				return;
			}
			if (TowerDefenseModeController.TryGetExistingInstance(out TowerDefenseModeController controller) == false)
			{
				Debug.LogError(TAG + " PLACE-FAIL controller 없음");
				return;
			}

			TowerDefensePlacement placement = controller.GetComponent<TowerDefensePlacement>();
			// 화면 좌표 계산은 **실제 렌더 카메라** 기준이어야 한다 — 개척이 정식 content 카메라가 되면서
			// 렌더 카메라는 Cinemachine brain 이 물고 있는 단 하나다(전용 Camera 자식은 폐기됨).
			Camera modeCamera = ViewCameraResolver.Current;
			if (placement == null || modeCamera == null)
			{
				Debug.LogError(TAG + " PLACE-FAIL placement=" + (placement != null) + " renderCamera=" + (modeCamera != null));
				return;
			}
			Debug.Log(TAG + " PLACE-VIA-SCREEN renderCamera=" + modeCamera.name + " pos=" + modeCamera.transform.position);

			int before = match.Resource;

			// 방어인형 — 판이 매 매치 새로 생성되므로 고정 좌표는 암반 위일 수 있다(그러면 배치가 조용히
			// 전부 거절돼 "방어 없는 판"을 방어 있는 판으로 착각한다). 코어 주변에서 *실제로 설 수 있는* 칸을 찾는다.
			// 종류를 섞어 세운다 — 한 종류만 세우면 광역·관통·둔화가 통째로 미검증으로 남는다.
			// 두 번의 배치(최초/재시작)에서 서로 다른 두 쌍을 세운다 — 한 쌍만 세우면 나머지 두 종류의
			// 효과가 통째로 미검증으로 남는다. 예산 160 안에서 각각 성립하는 조합.
			// 관측 구간(재시작 뒤)에 서 있는 쪽이 검증 대상이다 — 첫 판에 세운 포탑은 재시작이 치운다.
			// 그래서 *두 번째* 조합에 아직 미확인인 종류를 넣는다(관통은 직전 실행에서 확인됨).
			// ★ 확인할 항목(승급·함정·벽·연구)이 여럿인데 예산은 유한하다 — 초기 배치가 다 쓰면
			//   나머지가 전부 「돈이 없어 못 함」으로 끝나 *기능이 아니라 잔고*를 검사하게 된다.
			//   그래서 초기엔 한 기만 세우고 남은 예산을 항목들이 나눠 쓴다.
			int[] slotPlan = towerPlanFlipped ? new[] { 1 } : new[] { 0 };
			towerPlanFlipped = towerPlanFlipped == false;
			int towersPlaced = 0;
			List<Vector3> spots = FindPlaceableSpots(stageRoot, slotPlan.Length);
			for (int index = 0; index < spots.Count; index++)
			{
				int slot = slotPlan[index % slotPlan.Length];
				placement.SelectSlot(slot);
				int beforeTower = match.Resource;
				placement.PlaceTowerAt(WorldToScreen(modeCamera, stageRoot.TransformPoint(spots[index])));
				if (match.Resource < beforeTower)
					towersPlaced++;
			}
			// 시간 조작 — 멈춤·배속이 실제로 걸리는가(화면 버튼과 같은 경로).
			match.TogglePause();
			float paused = match.SpeedScale;
			match.TogglePause();
			match.CycleSpeed();
			Debug.Log(TAG + " TIME paused=" + paused.ToString("F0") + " cycled=" + match.SpeedScale.ToString("F0")
				+ " timeScale=" + Time.timeScale.ToString("F0"));
			match.CycleSpeed(); match.CycleSpeed(); // ×1 로 되돌림(순환).

			placement.SelectSlot(match.TowerArchetypeCount); // 채집 칸으로 되돌림.
			if (spots.Count > 0)
			{
				sellProbeLocal = spots[0];
				// ★ *어떤 종류를 세웠는지*도 같이 적어둔다 — 판마다 종류를 번갈아 세우면서 승급은 늘 0번으로
				//   걸고 있었다. 그러면 다른 종류라 규칙대로 거절되는데 하네스는 「승급이 안 된다」고 적는다.
				sellProbeSlot = slotPlan[0];
				sellProbeReady = true;
			}
			Debug.Log(TAG + " PLACE-TOWERS placed=" + towersPlaced
				+ " towerKinds=" + match.TowerArchetypeCount);

			// 채집인형 = 자원 노드 위. 좌표는 **스테이지 정본에서 읽는다** — 하네스에 박아두면 노드를
			// 옮기는 순간 "노드 위 배치" 검사가 조용히 "빈 땅 배치(항상 거절)" 로 바뀌어 무의미해진다.
			// ★ 절차 생성이면 노드가 매 판 다르다 — 스테이지 SO 의 고정 좌표를 읽으면 항상 빈 땅을 찍는다.
			IReadOnlyList<Vector3> nodeLocals = match.ActiveResourceNodeLocalPositions;
			if (nodeLocals.Count == 0)
				Debug.LogError(TAG + " PLACE-FAIL 스테이지에 자원 노드가 없음");
			// ★ 노드 전부에 세우면(6곳 × 60) 예산이 통째로 사라져 뒤의 확인이 전부 「돈이 없어 못 함」이 된다.
			//   여기서 볼 것은 「노드 위에 서는가」이므로 한 기면 충분하다.
			// ★ 채집 스폰은 코루틴(1프레임 양보 후 수입 반영)이라 *세운 그 틱에 읽으면 0*이다.
			//   그래서 확인(VerifyEssence)이 아니라 여기서 미리 세운다 — 1.5초 뒤에 읽힌다.
			//   바깥 노드(배수 큰 곳)를 우선 — 정수는 거기서만 난다.
			// ★ 돈이 모자라 못 세운 것을 「규칙이 막았다」와 구분할 수 없다 — 실측에서 둘째 채집이 늘
			//   거절돼 정수 경로가 매번 「확인 못 함」으로 끝났다. 값만 채워두고, *배치 규칙은 그대로* 둔다
			//   (보급·암반·점유는 안 건드린다 — 그걸 우회하면 확인 자체가 거짓이 된다).
			// 정수도 채운다 — 바깥 노드는 *전초기지로 보급을 늘려야* 닿는데 그게 정수를 쓴다.
			// 판에서는 둥지를 부수면 정수가 나므로 막힌 설계가 아니지만, 하네스는 그 시간을 못 기다린다.
			match.GrantForVerification(2000, 200);

			int harvestersPlaced = 0;
			nodeOrder.Clear();
			for (int index = 0; index < nodeLocals.Count; index++)
				nodeOrder.Add(index);
			// ★ 이제 「보급이 닿는 곳에만」 지을 수 있다 — 먼 노드부터 노리면 전부 거절돼 채집이 0 이 되고,
			//   그러면 정수·보급 확인이 통째로 무의미해진다(라이브 실증: 거절 로그만 쌓였다).
			//   사람이 하는 순서대로 *코어에서 가까운 것부터* 잡는다.
			Vector3 coreLocal = stageRoot.InverseTransformPoint(match.CoreCombatant != null
				? match.CoreCombatant.Position
				: stageRoot.position);
			nodeOrder.Sort((left, right) =>
				(nodeLocals[left] - coreLocal).sqrMagnitude.CompareTo((nodeLocals[right] - coreLocal).sqrMagnitude));

			Vector3 firstHarvesterLocal = Vector3.zero;
			foreach (int nodeIndex in nodeOrder)
			{
				Vector3 local = nodeLocals[nodeIndex];
				// ★ 가까운 것 하나 + 그보다 먼 것 하나 = 「이어지는가」와 「바깥에서 정수가 나오는가」를
				//   한 판에서 같이 본다. 가까운 것만 잡으면 정수 확인이 영영 「바깥에 세운 게 없음」으로만 끝난다.
				if (harvestersPlaced >= 2)
					break;
				// 둘째는 첫째와 충분히 떨어진 곳으로 — 붙여 세우면 같은 광맥을 물어 바깥 확인이 안 된다.
				if (harvestersPlaced == 1 && (nodeLocals[nodeIndex] - firstHarvesterLocal).sqrMagnitude < 400f)
					continue;
				int beforeHarvester = match.Resource;
				placement.PlaceHarvesterAt(WorldToScreen(modeCamera, stageRoot.TransformPoint(local)));
				if (match.Resource < beforeHarvester)
				{
					if (harvestersPlaced == 0)
						firstHarvesterLocal = local;
					harvestersPlaced++;
				}
			}

			// ★ 바깥으로 한 걸음 — 전초기지가 새 보급 원점이 된다. 이걸 안 하면 먼 노드는 영영 거절돼
			//   「바깥 채집이 정수를 내는가」가 매 판 확인 못 함으로 끝난다(실측: 여덟 판 내리 그랬다).
			if (harvestersPlaced > 0 && match.Essence >= match.Stage.OutpostEssenceCost)
			{
				// ★ 「멀다」로 고르면 안 된다 — 거리로 여덟 판을 골랐는데 전부 안쪽 등급이었다(판에 바깥
				//   광맥이 25개나 있는데도). 등급을 판에 직접 묻는다.
				List<Vector3> outerNodes = new List<Vector3>();
				match.CollectOuterNodeLocalPositions(outerNodes);
				if (outerNodes.Count == 0)
				{
					Debug.LogError(TAG + " OUTER-HARVEST-FAIL 판에 바깥 광맥이 없다.");
				}
				else
				{
					// ★ 광맥 좌표는 *무대 기준*이다 — 코어의 월드 좌표와 그냥 빼면 거리가 1900 이 나온다
					//   (지도는 250 남짓인데). 한 공간으로 맞춰서 잰다.
					outerNodes.Sort((left, right) =>
						(left - coreLocal).sqrMagnitude.CompareTo((right - coreLocal).sqrMagnitude));
					Vector3 targetLocal = outerNodes[0]; // 가장 가까운 바깥 광맥 — 사람도 여기부터 뻗는다.

					// 보급 원점을 그 광맥 쪽으로 한 걸음씩 — 한 번에 못 닿으면 여러 걸음 놓는다.
					for (int step = 1; step <= 4 && match.Essence >= match.Stage.OutpostEssenceCost; step++)
					{
						Vector3 towardLocal = Vector3.Lerp(coreLocal, targetLocal, step / 5f);
						match.TryPlaceOutpost(stageRoot.TransformPoint(towardLocal));
					}

					Debug.Log(TAG + " OUTPOST-REACH 바깥 광맥까지 보급 뻗기 · 전초기지 " + match.OutpostCount
						+ "개 · 목표거리 " + (targetLocal - coreLocal).magnitude.ToString("F1")
						+ " · 보급거리 " + match.EffectiveSupplyReach.ToString("F1"));

					int beforeFar = match.Resource;
					placement.PlaceHarvesterAt(WorldToScreen(modeCamera, stageRoot.TransformPoint(targetLocal)));
					bool outerPlaced = match.Resource < beforeFar;
					Debug.Log(TAG + " OUTER-HARVEST 바깥 광맥 채집 "
						+ (outerPlaced ? "성공" : "거절됨(보급 미도달)"));

					// ★ 보급과 전기는 *다른 관문*이다 — 이어져도 전기가 안 닿으면 정수는 0 이다.
					//   여기까지 안 놓으면 확인이 늘 「이어졌지만 전기가 안 닿음」에서 멈춘다(실측).
					if (outerPlaced)
					{
						int beforeGenerator = match.Resource;
						Vector3 besideNode = targetLocal + new Vector3(match.Stage.GeneratorRadius * 0.4f, 0f, 0f);
						match.TryPlaceGenerator(stageRoot.TransformPoint(besideNode));
						if (match.Resource == beforeGenerator)
							match.TryPlaceGenerator(stageRoot.TransformPoint(
								targetLocal - new Vector3(match.Stage.GeneratorRadius * 0.4f, 0f, 0f)));
						Debug.Log(TAG + " OUTER-POWER 바깥 채집 옆에 발전 인형 "
							+ (match.Resource < beforeGenerator ? "세움" : "못 세움")
							+ " · 전기 반경 " + match.Stage.GeneratorRadius.ToString("F1"));
					}
				}
			}

			// ★ 징검다리 — 먼 노드는 코어에서 한 번에 안 닿는다. 사람이 하는 일(중간에 하나 세워 잇기)을
			//   하네스도 해야 「사슬이 실제로 도는가」를 볼 수 있다. 안 하면 늘 「끊김」만 보고 규칙이
			//   고장난 줄 알게 된다(실측: 정수 0 이 계속 나왔는데 원인은 사슬 미구축이었다).
			if (harvestersPlaced > 0)
			{
				placement.SelectSlot(0);
				Vector3 bridgeLocal = firstHarvesterLocal * 0.5f; // 코어(원점)와 노드의 중간.
				placement.PlaceTowerAt(WorldToScreen(modeCamera, stageRoot.TransformPoint(bridgeLocal)));
				Debug.Log(TAG + " SUPPLY-BRIDGE 중간에 하나 세워 사슬 시도 local=" + bridgeLocal);
			}

			// 노드에서 먼 빈 땅에 채집 시도 = 거절돼야 정상(노드 결합 규칙 살아있음 확인).
			int beforeOffNode = match.Resource;
			placement.PlaceHarvesterAt(WorldToScreen(modeCamera, stageRoot.TransformPoint(new Vector3(0f, 0f, 2f))));
			bool offNodeRejected = match.Resource == beforeOffNode;

			Debug.Log(TAG + " PLACE resourceBefore=" + before + " after=" + match.Resource
				+ " offNodeHarvesterRejected=" + offNodeRejected);


			VerifyHeroAndNames(stageRoot);

			LogHudState();
			LogNodeMarkers(stageRoot);
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

		/// <summary>
		/// 서식지가 깔렸는가 + 파도가 테두리 토막에서 오는가 — 둘 다 「판에 있어야」 의미가 있다.
		/// </summary>
		private static void VerifyLairsAndInvasion()
		{
			if (match == null)
				return;

			int lairCount = match.SleepingLairCount;
			string nextDirection = match.IsBorderInvasion ? match.NextInvasionDirectionName() : "(꺼짐)";

			Debug.Log($"{TAG} 서식지 {lairCount}곳 · 테두리 침공 {match.IsBorderInvasion} · 다음 파도 {nextDirection}쪽");

			if (lairCount == 0)
				Debug.LogError($"{TAG} 서식지 FAIL — 0곳. 넓히는 것이 위험이 되는 층이 통째로 빠졌다.");
			if (match.IsBorderInvasion && string.IsNullOrEmpty(nextDirection))
				Debug.LogError($"{TAG} 예고 FAIL — 다음 파도 방향을 못 말한다. 예고가 성립하지 않는다.");
		}

		/// <summary>
		/// 예고 표식과 경고 표식이 **화면에 실제로 떠 있는가**.
		///
		/// ★ 여태 이 둘은 「코드가 있다」까지만 확인됐다. 화면 층에 글자가 안 붙으면 규칙이 아무리
		///   맞아도 사람에겐 없는 기능이다(도달 불가). 뜬 개수를 직접 센다.
		/// ★ 경고는 사건이 나야 뜨므로 검증용으로 하나 띄우고, 그것이 화면까지 가는지만 본다.
		/// </summary>
		private static void VerifyOnScreenMarks()
		{
			if (match == null)
				return;

			match.RaiseAlertForVerification("검증 알림");

			// ★ 곁눈질용 미니맵은 마우스 설명을 안 단다(그 아래 땅을 눌러야 하므로). 설명이 붙는 것은
			//   펼친 지도뿐이라, 「서식지가 서식지로 읽히는가」는 지도를 열어야만 잴 수 있다.
			if (TowerDefenseModeController.TryGetExistingInstance(out TowerDefenseModeController mapOwner))
				mapOwner.OpenMapForVerification();

			// ★ 서식지 표시는 *밝힌 곳만* 뜬다 — 안 밝히면 이 검사는 영영 「못 쟀다」로 끝난다.
			//   한 곳만 밝혀서 「밝히면 서식지로 뜨는가」를 실제로 재게 만든다.
			foreach (TowerDefenseMatch.LairMarker lair in match.LairMarkers)
			{
				match.RevealForVerification(lair.Position, 6f);
				break;
			}
			// ★ 한 틱 미루는 것으로는 부족하다 — 에디터가 앞에 없으면 Play 루프가 느려져 *게임 프레임이
			//   한 장도 안 지난 채* 재게 된다(오늘 세 번째로 같은 실수를 했다). 실제 시간이 흐른 뒤에 센다.
			markCheckAt = EditorApplication.timeSinceStartup + 1.5;

			// ★ 강도는 *시간이* 올린다 — 짧은 하네스에서는 1.02 까지밖에 안 올라 알림 조건에 영영 안 닿는다.
			//   새 훅을 만들 것 없이 **사람이 쓰는 배속 버튼**을 눌러 시간을 빨리 흐르게 한다
			//   (검증 전용 통로를 또 파면 「실제로 도는 길」과 「재는 길」이 갈라진다).
			for (int step = 0; step < 3; step++)
				match.CycleSpeed();
			pressureCheckAt = EditorApplication.timeSinceStartup + 75.0;
			pressureBefore = match.Pressure;
		}

		private static double pressureCheckAt;
		private static float pressureBefore;

		/// <summary> 시간이 흐르면 강도가 오르고, 오르면 화면이 말하는가. </summary>
		private static void CheckPressureNotice()
		{
			if (match == null)
				return;

			float now = match.Pressure;
			int alerts = 0;
			foreach (TowerDefenseAlerts.Alert alert in match.Alerts)
			{
				if (alert.Label.Contains("단단해졌다"))
					alerts++;
			}

			Debug.Log($"{TAG} 강도 알림 — 강도 {pressureBefore:F2} → {now:F2} · 「더 단단해졌다」 알림 {alerts}개");

			if (now <= pressureBefore + 0.01f)
				Debug.Log(TAG + " 강도 알림 — 못 쟀다(배속을 올려도 강도가 안 올랐다). 실패가 아니다.");
			else if (now - 1f < 0.5f)
				Debug.Log($"{TAG} 강도 알림 — 못 쟀다(아직 한 칸 못 올랐다: {now - 1f:F2}/0.50). 실패가 아니다.");
			else if (alerts == 0)
				Debug.LogError(TAG + " 강도 FAIL — 강도가 한 칸 넘게 올랐는데 화면이 아무 말도 안 한다.");

			// ★ 깨어난 서식지 마수가 *어디로 가는지*를 잰다. 코어로 행진하면 서식지는 그냥 「파도 하나 더」이고,
			//   그 일대에 머물면 「넓히는 것이 위험」이 성립한다 — 둘은 완전히 다른 게임이라 재봐야 안다.
			if (match.WakeNearestLairForVerification(out Vector3 wokenAt))
			{
				lairWakeFrom = match.AwakenedGuardDistanceToCore(out lairWakeGuards);
				lairWakePosition = wokenAt;
				lairDriftCheckAt = EditorApplication.timeSinceStartup + 5.0;
				lairWakeMatch = match; // ★ 잰 판이 깨운 판과 같은지 확인용 — 다르면 비교 자체가 무의미하다.
				lairWakeLives = match.Lives;
				lairWakeEnemies = match.WaveEnemies.Count;
				Debug.Log($"{TAG} 서식지 강제 기상 — 마수 {lairWakeGuards}기 · 코어까지 {lairWakeFrom:F1}"
					+ $" · 목숨 {lairWakeLives} · 판 위 마수 {lairWakeEnemies}");
			}
		}

		private static double lairDriftCheckAt;
		private static float lairWakeFrom;
		private static int lairWakeGuards;
		private static TowerDefenseMatch lairWakeMatch;
		private static int lairWakeLives;
		private static int lairWakeEnemies;
		private static Vector3 lairWakePosition;

		/// <summary> 깨운 마수가 8초 동안 코어 쪽으로 얼마나 다가갔나 — 「행진」과 「지킴」을 가른다. </summary>
		private static void CheckLairDrift()
		{
			if (match == null)
				return;

			// ★ 깨운 판과 지금 판이 다르면 비교가 성립하지 않는다 — 모드를 나갔다 들어오면 판이 새로 태어나
			//   서식지도 전부 다시 잠든 채로 깔린다. 그걸 모르고 재면 「깨운 마수가 전부 사라졌다」는
			//   *존재하지 않는 결함*을 보고하게 된다(실제로 그렇게 두 사이클을 썼다).
			if (lairWakeMatch == null || match != lairWakeMatch)
			{
				Debug.Log(TAG + " 서식지 이동 — 잴 수 없음(판이 그 사이에 새로 시작됐다). 이번 회차는 건너뛴다.");
				return;
			}

			float now = match.AwakenedGuardDistanceToCore(out int aliveNow, out int destroyedNow, out int disabledNow);
			Debug.Log($"{TAG} 서식지 이동 — 마수 {lairWakeGuards}기 → 살아있음 {aliveNow} · 파괴됨 {destroyedNow} · 꺼짐 {disabledNow}"
				+ $" · 코어까지 {lairWakeFrom:F1} → {now:F1}");

			// ★ 살아남은 것이 없으면 거리는 뜻이 없다 — 「가까워졌다」가 아니라 「죽어서 없다」다.
			if (aliveNow == 0)
			{
				// ★ 「죽었다」와 「유출로 사라졌다」와 「무대 밖으로 치워졌다」는 고치는 자리가 전부 다르다.
				//   목숨이 줄었으면 유출, 안 줄었으면 죽거나 치워진 것 — 그 둘을 여기서 가른다.
				Debug.LogError($"{TAG} 서식지 FAIL — 깨운 마수 {lairWakeGuards}기가 8초 만에 전부 사라졌다"
					+ $" (목숨 {lairWakeLives}→{match.Lives} · 판 위 마수 {lairWakeEnemies}→{match.WaveEnemies.Count}"
					+ $" · 파괴 {destroyedNow} · 꺼짐 {disabledNow})."
					+ " 코어에서 멀어 포탑에 죽은 것이 아니다 — 서식지가 판에 아무 영향을 못 준다.");
				return;
			}

			// ★ 판정은 **집에서 얼마나 멀어졌나**로 한다 — 코어까지의 거리로 재면 그 서식지가 원래
			//   코어에 가까웠는지 멀었는지에 답이 좌우된다(같은 행동이 판마다 통과·실패로 갈린다).
			float fromHome = match.AwakenedGuardDistanceFromHome();
			float leash = TowerDefenseModeControllerLeash();
			Debug.Log($"{TAG} 서식지 목줄 — 집에서 최대 {fromHome:F1} (목줄 {leash:F1})"
				+ $" · 깨운 {match.LairsAwakened}곳 · 쓸어낸 {match.LairsCleared}곳 · 정수 {match.Essence}");

			if (leash > 0f && fromHome > leash * 1.5f)
			{
				Debug.LogError($"{TAG} 서식지 FAIL — 집에서 {fromHome:F1} 까지 벗어났다(목줄 {leash:F1})."
					+ " 「넓히는 것이 위험」이 아니라 파도가 하나 더 있는 것이다.");
			}

			// ★ 보상은 *다 죽어야* 나오는데 하네스는 전투로 그걸 못 만든다 — 조건만 만들어 규칙을 확인한다.
			int essenceBefore = match.Essence;
			int clearedBefore = match.LairsCleared;
			if (match.ClearAwakenedLairForVerification())
				lairClearCheckAt = EditorApplication.timeSinceStartup + 1.0;
			lairClearEssenceBefore = essenceBefore;
			lairClearBefore = clearedBefore;
		}

		private static double lairClearCheckAt;
		private static int lairClearEssenceBefore;
		private static int lairClearBefore;

		/// <summary> 서식지를 다 쓸면 정수가 들어오나 — 「싸워서 버는 길」이 실제로 이어져 있는지. </summary>
		private static void CheckLairClearReward()
		{
			if (match == null)
				return;

			int gained = match.Essence - lairClearEssenceBefore;
			int clearedNow = match.LairsCleared - lairClearBefore;
			Debug.Log($"{TAG} 서식지 소탕 보상 — 쓸어낸 곳 +{clearedNow} · 정수 +{gained}");

			if (clearedNow <= 0)
				Debug.LogError(TAG + " 소탕 FAIL — 다 쓸었는데 「쓸어낸 서식지」가 안 는다.");
			else if (gained <= 0)
				Debug.LogError(TAG + " 소탕 FAIL — 쓸었다고 세는데 정수가 한 푼도 안 들어온다.");
		}

		/// <summary> 지금 스테이지의 목줄 반경 — 판정 기준을 규칙에서 그대로 읽는다(하네스가 따로 박지 않는다). </summary>
		private static float TowerDefenseModeControllerLeash()
		{
			TowerDefenseModeController controller = Object.FindAnyObjectByType<TowerDefenseModeController>();
			return controller != null && controller.Stage != null ? controller.Stage.LairLeashRadius : 0f;
		}

		private static void CountOnScreenMarks()
		{
			{
				UIDocument document = Object.FindAnyObjectByType<UIDocument>();
				if (document == null || document.rootVisualElement == null)
				{
					Debug.LogError(TAG + " 표식 FAIL — 화면 문서가 없다.");
					return;
				}

				string invasionSentence = string.Empty;
				int invasionMarks = 0;
				int alertMarks = 0;
				int alertMarksHidden = 0;
				int alertSlots = 0;
				foreach (VisualElement element in document.rootVisualElement.Query<Label>().Build())
				{
					Label label = (Label)element;
					string text = label.text;
					bool hidden = element.resolvedStyle.display == DisplayStyle.None;

					// 알림 칸은 *만들어졌는지*와 *글자가 들어갔는지*와 *보이는지*가 각각 다른 문제다.
					if (label.name == "AlertMark" || (string.IsNullOrEmpty(text) == false && text.Contains("❗")))
						alertSlots++;

					if (string.IsNullOrEmpty(text))
						continue;
					if (text.Contains("❗"))
					{
						if (hidden)
							alertMarksHidden++;
						else
							alertMarks++;
					}
					if (hidden)
						continue;
					if (text.Contains("▼") || text.Contains("에서 온다"))
						invasionMarks++;
					if (text.Contains("에서 온다"))
						invasionSentence = text;
				}

				// ★ 「규칙에 있나 / 화면에 갔나」를 갈라 찍는다 — 0 하나만 보면 어디서 끊겼는지 모른다.
				int ruleAlerts = match != null ? match.Alerts.Count : -1;
				// 미니맵이 서식지를 「마수」가 아니라 *서식지*로 말하는지 — 말이 틀리면 판단이 틀린다.
				int lairDots = 0;
				int lyingEnemyDots = 0;
				int mapDots = 0;
				int mapDotsWithTip = 0;
				foreach (VisualElement element in document.rootVisualElement.Query<VisualElement>().Build())
				{
					if (element.name == "MapDot" && element.resolvedStyle.display != DisplayStyle.None)
						mapDots++;

					string tip = element.tooltip;
					if (string.IsNullOrEmpty(tip))
						continue;
					if (element.name == "MapDot")
						mapDotsWithTip++;
					if (tip.StartsWith("서식지"))
						lairDots++;
					else if (tip.StartsWith("마수"))
						lyingEnemyDots++;
				}
				// ★ 「지도 점이 있나 / 설명이 붙었나 / 무엇이라 부르나」를 갈라 찍는다.
				//   숫자 하나만 보면 「지도가 없다」와 「이름이 틀렸다」가 똑같이 0 으로 보인다.
				// 예고가 「무엇이 오는가」까지 말하는가 — 방향만으로는 어떤 대비를 할지 못 정한다.
				string expectedPhrase = match != null ? match.NextWaveEventPhrase() : string.Empty;
				Debug.Log($"{TAG} 예고 문장 — 「{invasionSentence}」 (성격 「{expectedPhrase}」)");
				if (string.IsNullOrEmpty(expectedPhrase) == false
					&& string.IsNullOrEmpty(invasionSentence) == false
					&& invasionSentence.Contains(expectedPhrase.Trim()) == false)
				{
					Debug.LogError(TAG + " 예고 FAIL — 다음 파도에 성격이 있는데 예고가 방향만 말한다.");
				}

				// ★ 서식지가 *만나지는 층인가* — 만들어놓고 도달 불가면 그 층은 통째로 죽은 것이다.
				//   보급이 닿는 거리와 가장 가까운 서식지 거리를 나란히 놓고 본다.
				if (match != null)
				{
					float nearest = float.MaxValue;
					int lairTotal = 0;
					foreach (TowerDefenseMatch.LairMarker lair in match.LairMarkers)
					{
						lairTotal++;
						float distance = Vector3.Distance(lair.Position, match.CoreCombatant != null
							? match.CoreCombatant.Position
							: Vector3.zero);
						if (distance < nearest)
							nearest = distance;
					}

					float reach = match.EffectiveSupplyReach;
					Debug.Log($"{TAG} 서식지 도달 — {lairTotal}곳 · 가장 가까운 것 {(lairTotal > 0 ? nearest : -1f):F1}"
						+ $" · 보급 거리 {reach:F1} · 깨우는 거리 안에 들려면 {(lairTotal > 0 ? nearest - reach : 0f):F1} 더 나가야 한다");

					if (lairTotal > 0 && nearest > reach * 4f)
					{
						Debug.LogWarning($"{TAG} 서식지 도달 — 가장 가까운 서식지가 보급 거리의 {nearest / Mathf.Max(1f, reach):F1}배다."
							+ " 한 판에 한 번도 안 만나면 이 층은 없는 것과 같다.");
					}
				}

				// ★ 풀이 남의 상태를 기억하면 재사용된 마수가 꺼진 채로 태어나 영영 안 움직인다.
				//   한 마리만 굳어도 파도가 안 끝난다 — 0 이 아니면 그 자리에서 실패다.
				int frozen = match != null ? match.FrozenEnemyCount : 0;
				Debug.Log($"{TAG} 굳은 마수 — 전술 꺼진 채 살아있는 마수 {frozen}기");
				if (frozen > 0)
					Debug.LogError($"{TAG} 굳음 FAIL — {frozen}기가 전술이 꺼진 채로 살아 있다(풀에 상태가 새어 나갔다).");

				// ★ 길찾기 상한이 판에 비해 모자라면 마수가 「갈 길이 있는데」 못 간다 — 증상은
				//   「몇 마리가 그냥 안 움직인다」로만 보여서 원인을 길찾기로 짚기가 어렵다.
				if (match != null)
				{
					Debug.Log($"{TAG} 길찾기 — 판 {match.MapCellCount}칸 · 한 번에 최대 {match.PathPeakCells}칸 펼침"
						+ $" · 상한에 걸려 포기 {match.PathCapHits}회");
					if (match.PathCapHits > 0)
						Debug.LogError($"{TAG} 길찾기 FAIL — 상한에 걸려 {match.PathCapHits}회 포기했다(마수가 갈 길이 있는데 못 간다).");
				}

				// 강도는 시간이 올리는 규칙이다 — 「내 포탑이 약해졌다」로 오해하지 않으려면 보여야 한다.
				Debug.Log($"{TAG} 강도 — 마수 강도 {(match != null ? match.Pressure : 0f):F2}");

				// 적응은 판을 바꾸는 규칙인데 그리던 칸이 숨겨져 화면에서 사라졌었다 —
				// 「규칙이 말하는 것」과 「화면이 말하는 것」을 나란히 놓고 본다.
				string adaptation = match != null ? match.AdaptationNote : string.Empty;
				Debug.Log($"{TAG} 적응 — 규칙 「{adaptation}」 · 화면 경고 {alertMarks}개");
				if (string.IsNullOrEmpty(adaptation))
					Debug.Log(TAG + " 적응 — 못 쟀다(아직 아무것에도 안 익숙하다). 실패가 아니다.");

				bool mapOpen = TowerDefenseModeController.TryGetExistingInstance(out TowerDefenseModeController mapView)
					&& mapView.IsMapOpenForVerification;
				// ★ 「안 그려졌다」와 「그릴 게 없다」는 다르다 — 서식지는 *밝힌 곳만* 그린다(시야 규칙).
				//   밝힌 서식지가 0 곳이면 이 검사는 실패가 아니라 **못 잰 것**이다.
				int exploredLairs = 0;
				if (match != null)
				{
					foreach (TowerDefenseMatch.LairMarker lair in match.LairMarkers)
					{
						if (match.IsExploredAt(lair.Position))
							exploredLairs++;
					}
				}

				Debug.Log($"{TAG} 지도 — 열림 {mapOpen} · 점 {mapDots}개 · 설명 붙은 점 {mapDotsWithTip}개"
					+ $" · 서식지로 읽힘 {lairDots}개 (밝힌 서식지 {exploredLairs}곳) · 마수로 읽힘 {lyingEnemyDots}개");

				if (exploredLairs == 0)
					Debug.Log(TAG + " 지도 — 서식지 표시는 못 쟀다(아직 밝힌 서식지가 0곳). 실패가 아니다.");
				else if (lairDots == 0)
					Debug.LogError(TAG + " 지도 FAIL — 밝힌 서식지가 있는데 지도에 서식지로 안 뜬다.");

				Debug.Log($"{TAG} 화면 표식 — 파도 예고 {invasionMarks}개 · 경고 {alertMarks}개"
					+ $" (규칙층 알림 {ruleAlerts}개 · 글자든 알림칸 {alertSlots}개 · 숨은 경고 {alertMarksHidden}개)");
				if (invasionMarks == 0)
					Debug.LogError(TAG + " 예고 FAIL — 다음 파도 표식이 화면에 하나도 없다. 규칙만 맞고 사람에겐 안 보인다.");
				if (alertMarks == 0)
					Debug.LogError(TAG + " 경고 FAIL — 띄운 알림이 화면까지 안 갔다.");
			}
		}

		private static void LogHudState()
		{
			UIRoot uiRoot = Object.FindAnyObjectByType<UIRoot>();
			// ★ 개척 HUD 는 **ModeHudLayer** 에 붙는다(본편 HUD 를 통째 숨겨도 살아남아야 해서 한 단 위 층).
			//   검사기는 옛 층(OverlayLayer)을 보고 있어서 **매번 HUD-FAIL 을 뱉었다** — 화면엔 멀쩡히
			//   떠 있는데 확인 도구만 못 찾는 상태다. 이런 실패가 상시로 뜨면 사람이 로그를 통째로 무시하게 된다.
			// HudLayer 를 보던 예전 assert 는 그 설계 변경 이후로 항상 실패하는 죽은 검사였다.
			if (uiRoot == null || uiRoot.ModeHudLayer == null)
			{
				Debug.LogError(TAG + " HUD-FAIL UIRoot/ModeHudLayer 없음");
				return;
			}

			VisualElement hud = uiRoot.ModeHudLayer.Q(nameof(TowerDefenseHudView));
			if (hud == null)
			{
				Debug.LogError(TAG + " HUD-FAIL ModeHudLayer 에 TowerDefenseHudView 없음");
				return;
			}

			string statusText = string.Empty;
			foreach (Label label in hud.Query<Label>().ToList())
			{
				if (string.IsNullOrEmpty(label.text) == false)
				{
					statusText = label.text;
					break;
				}
			}
			Debug.Log(TAG + " HUD visible=" + (hud.style.display.value == DisplayStyle.Flex)
				+ " text=\"" + statusText + "\"");
		}

		// 자원 노드 표식 — 안 보이면 채집 인형을 어디 지을지 알 수 없다.
		private static void LogNodeMarkers(Transform stageRoot)
		{
			int markers = 0;
			foreach (Transform child in stageRoot)
			{
				if (child.name == "ResourceNode")
					markers++;
			}
			Debug.Log(TAG + " NODE-MARKERS count=" + markers);
		}

		/// <summary>
		/// 티메토 허브 라이브 확인 (TASK-WM-195) — 씬의 티메토 NPCObject 를 찾아 실제 대화 진입점
		/// `OnInteract()` 를 호출하고, 허브 패널이 열려 미니게임 목록이 렌더됐는지 본다.
		/// 에디터에서 데이터만 보는 건 "말 걸면 뜬다"의 증명이 아니다(사용자가 실제로 못 찾은 사례).
		/// </summary>
		private static void VerifyTimetoHub()
		{
			NPCObject[] npcs = Object.FindObjectsByType<NPCObject>(FindObjectsInactive.Include);
			NPCObject timeto = null;
			foreach (NPCObject npc in npcs)
			{
				if (npc.Data != null && npc.Data.ID == 7)
				{
					timeto = npc;
					break;
				}
			}

			if (timeto == null)
			{
				Debug.LogError(TAG + " HUB-FAIL 씬에 티메토(NPC ID 7) 없음 — 씬 스캔 " + npcs.Length + "명");
				return;
			}

			List<MinigameEntrySO> entries = NPCUtil.GetMinigameEntries(timeto.Data);
			Debug.Log(TAG + " HUB-NPC 티메토 발견 panels=" + string.Join(",", timeto.Data.GetPanelTypeList())
				+ " entries=" + entries.Count);

			// 실제 대화 진입점 — 플레이어가 상호작용했을 때와 동일 경로.
			timeto.OnInteract();

			// 대화 메뉴에 「시뮬레이션 콘솔」 선택지가 실제로 켜졌는지 = 사용자가 도달 가능한가의 핵심.
			// 메뉴 버튼은 NPCPanelType.Count 만큼 동적 생성되고 NPC 의 PanelInfos 로 활성 여부가 갈린다.
			UINPCMenu menu = Object.FindAnyObjectByType<UINPCMenu>(FindObjectsInactive.Include);
			if (menu == null)
			{
				Debug.LogError(TAG + " HUB-MENU UINPCMenu 없음");
			}
			else
			{
				int activeOptions = 0;
				foreach (UISlot slot in menu.GetComponentsInChildren<UISlot>(true))
				{
					if (slot.gameObject.activeSelf && slot.Index == (int)NPCPanelType.Hub)
						activeOptions++;
				}
				Debug.Log(TAG + " HUB-MENU hubOptionActive=" + (activeOptions > 0));
			}

			UIRoot uiRoot = Object.FindAnyObjectByType<UIRoot>();
			VisualElement hubPanel = uiRoot != null && uiRoot.ScreenLayer != null
				? uiRoot.ScreenLayer.Q(nameof(UIMinigameHubToolkit))
				: null;
			Debug.Log(TAG + " HUB-PANEL exists=" + (hubPanel != null)
				+ " buttons=" + (hubPanel != null ? hubPanel.Query<Button>().ToList().Count : -1));

			// 허브를 닫고 원래 흐름(TD 모드 진입)으로 복귀 — 패널이 열린 채면 입력이 UI 에 묶인다.
			if (UIManagerHubCloseSafe(uiRoot) == false)
				Debug.LogWarning(TAG + " HUB 패널 닫기 실패 — 이후 검증이 UI 에 막힐 수 있음");
		}

		private static bool UIManagerHubCloseSafe(UIRoot uiRoot)
		{
			UIManager uiManager = Object.FindAnyObjectByType<UIManager>();
			if (uiManager == null || uiManager.NPC == null)
				return false;
			uiManager.NPC.ClosePanel();
			return true;
		}

		/// <summary>
		/// 카메라 실측 — "GameObject 가 active" 는 *화면에 보인다*의 증명이 아니다(사용자 실증:
		/// camActive=True 였는데 화면은 그대로였음). 실제로 무엇이 렌더되는지는 enabled + depth +
		/// URP renderType(Base/Overlay) + Camera.main 이 함께 결정하므로 전부 찍는다.
		/// </summary>
		private static void DumpCameras(string phase)
		{
			// ⚠ Unity 콘솔 리더는 멀티라인 로그의 *첫 줄만* 준다 → 카메라마다 별도 Debug.Log 로 찍어야
			//   원격(MCP)에서 전부 읽힌다. 한 줄로 몰면 진단이 통째 유실됨(실측).
			Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude);
			Debug.Log(TAG + " CAMERAS(" + phase + ") count=" + cameras.Length
				+ " main=" + (Camera.main != null ? Camera.main.name : "NULL"));

			Camera topmost = null;
			foreach (Camera camera in cameras)
			{
				string renderType = "n/a";
				UnityEngine.Rendering.Universal.UniversalAdditionalCameraData urpData =
					camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
				if (urpData != null)
					renderType = urpData.renderType.ToString();

				Debug.Log(TAG + " CAM[" + phase + "] " + camera.name
					+ " enabled=" + camera.enabled
					+ " active=" + camera.gameObject.activeInHierarchy
					+ " depth=" + camera.depth
					+ " urpType=" + renderType
					+ " pos=" + camera.transform.position);

				if (camera.enabled == false)
					continue;
				if (topmost == null || camera.depth > topmost.depth)
					topmost = camera;
			}

			// ★ 개척은 이제 **정식 content 카메라**(vcam priority)다 — "카메라를 하나 더 켰나"가 아니라
			//   ① 렌더 카메라가 하나뿐인가 ② content 모드가 개척으로 바뀌었나 ③ 개척 vcam 이 등록·구동 중인가
			//   를 봐야 한다. 예전 assert(ModeCamera 가 최상위인가)는 구조가 바뀌어 무의미해졌다.
			Debug.Log(TAG + " CAM-RENDER[" + phase + "] renderCameraCount=" + cameras.Length
				+ " topmost=" + (topmost != null ? topmost.name + "(depth " + topmost.depth + ")" : "none"));

			CameraManager cameraManager = CameraManager.Instance;
			if (cameraManager == null)
			{
				Debug.LogError(TAG + " CAM-MODE[" + phase + "] CameraManager.Instance NULL — 카메라 리그 자체가 없음.");
				return;
			}

			Debug.Log(TAG + " CAM-MODE[" + phase + "] contentMode=" + cameraManager.CurrentContentMode
				+ " isFreePosition=" + cameraManager.IsFreePositionMode);

			// 개척 vcam 실재/등록 확인 — 없으면 SetContentCameraMode 가 First() 에서 터지거나 무시된다.
			MCamera[] allVcams = Object.FindObjectsByType<MCamera>(FindObjectsInactive.Include);
			bool foundTowerDefenseVcam = false;
			foreach (MCamera vcam in allVcams)
			{
				Debug.Log(TAG + " VCAM[" + phase + "] " + vcam.name
					+ " contentMode=" + vcam.ContentCameraMode
					// priority 는 Cinemachine 타입이라 Editor asmdef 에서 직접 못 읽는다 — 대신 활성/좌표로 판별.
					+ " active=" + vcam.gameObject.activeInHierarchy
					+ " pos=" + vcam.transform.position);
				if (vcam.ContentCameraMode == ContentCameraMode.TowerDefense)
					foundTowerDefenseVcam = true;
			}

			if (foundTowerDefenseVcam == false)
				Debug.LogError(TAG + " VCAM-MISS[" + phase + "] 개척 vcam(ContentCameraMode.TowerDefense) 이 씬에 없음 — Camera 프리팹 자식 Camera_TowerDefense 확인 필요.");
			else if (cameraManager.CurrentContentMode != ContentCameraMode.TowerDefense && phase.Contains("진입"))
				Debug.LogError(TAG + " CAM-MODE-MISS[" + phase + "] 개척 진입인데 contentMode=" + cameraManager.CurrentContentMode + " — 전환 실패.");
		}

		private const double STUCK_ASSAULT_SECONDS = 25.0;
		private static double assaultStart = -1.0;
		private static bool stuckDumped;
		// 집계가 마지막으로 *움직인* 시점을 잡기 위한 직전 값 — 실시간에는 이 정체가 곧 고착이다.
		private static int lastAliveEnemyCount = -1;

		/// <summary> 코어가 "살아있다"고 세는 적을 전부 찍는다 — 화면과 대조해 유령/고착을 가른다. </summary>
		/// <summary>
		/// 판이 고착됐나 — 「마릿수가 한참 그대로」 하나로 판정한다.
		///
		/// ★ 두 가지를 여기서 못 박는다:
		///   ① *판 시계*로 잰다 — 사람이 멈추거나 느리게 해두면 에디터 시계만 흐르고 판은 그대로다.
		///   ② *한 곳*에서만 판정한다 — 예전엔 관찰 구간마다 사본이 있었고, 그중 하나가 「교전 중이면
		///      30초」로 재고 있었다. 실시간에는 늘 교전 중이라 그 사본은 **매 판 무조건** 경고를 찍었다
		///      (실측: 게임은 멀쩡히 1.2초마다 마수를 내보내는데 「30초째 정체」라고 적혔다).
		///      거짓 경고는 진짜 고착을 묻는다.
		/// </summary>
		private static void CheckStall()
		{
			if (match == null)
				return;

			int aliveTracked = match.AliveEnemyCount;
			float matchClock = match.SurvivedSeconds;

			if (aliveTracked != lastAliveEnemyCount)
			{
				lastAliveEnemyCount = aliveTracked;
				assaultStart = matchClock;
				stuckDumped = false;
				return;
			}

			if (aliveTracked <= 0)
				return;

			if (assaultStart < 0)
			{
				assaultStart = matchClock;
				return;
			}

			if (matchClock - assaultStart > STUCK_ASSAULT_SECONDS && stuckDumped == false)
			{
				stuckDumped = true;
				DumpWaveEnemies(matchClock - assaultStart);
			}
		}

		private static void DumpWaveEnemies(double elapsed)
		{
			MatchCombatant core = match.CoreCombatant;
			Debug.LogWarning(TAG + " STUCK-ASSAULT 판 시계 " + elapsed.ToString("F1") + "s 동안 마릿수 그대로 — wave=" + match.WaveIndex
				+ " coreAliveCount=" + match.AliveEnemyCount + " tracked=" + match.WaveEnemies.Count
				+ " 판시계=" + match.SurvivedSeconds + "s timeScale=" + Time.timeScale.ToString("F1"));

			// ★ 교착의 두 갈래를 가른다: 적이 코어를 *때리고 있는데 안 죽는* 것인가(코어 체력이 줄고 있다),
			//   아니면 *아예 안 때리는* 것인가(체력 그대로 = 아무도 아무것도 안 함 = 영구 교착).
			Debug.Log(TAG + " STUCK-CORE alive=" + (core != null && core.IsAlive)
				+ " hp=" + (core != null ? core.Hp + "/" + core.HpMax : "n/a"));

			int defendersAlive = 0;
			foreach (ICombatant combatant in match.RegisteredCombatants)
			{
				if (combatant == null || combatant.TeamId != 0 || combatant.IsAlive == false)
					continue;
				defendersAlive++;
				Debug.Log(TAG + " STUCK-DEFENDER id=" + combatant.CombatantId
					+ " hp=" + combatant.Hp + "/" + combatant.HpMax + " pos=" + combatant.Position);
			}
			Debug.Log(TAG + " STUCK-DEFENDERS aliveCount=" + defendersAlive + " (코어 포함)");

			for (int index = 0; index < match.WaveEnemies.Count; index++)
			{
				MatchCombatant enemy = match.WaveEnemies[index];
				if (enemy == null)
				{
					Debug.Log(TAG + " STUCK-ENEMY[" + index + "] null(파괴됨)");
					continue;
				}

				Debug.Log(TAG + " STUCK-ENEMY[" + index + "] alive=" + enemy.IsAlive
					+ " hp=" + enemy.Hp + "/" + enemy.HpMax
					+ " activeInHierarchy=" + enemy.gameObject.activeInHierarchy
					+ " pos=" + enemy.transform.position
					+ " driver=" + (enemy.GetComponent<TacticDriver>() != null));
			}
		}

		/// <summary>
		/// 배치된 수비 유닛의 **초기화 상태 전량** 덤프 — 재시작 후 풀에서 되뽑힌 개체가
		/// 지난 판의 흔적(색·크기·애니메이터·드라이버·체력)을 뒤집어쓰지 않았는지 좌표와 함께 본다.
		/// 사용자 실증: "재시작할 때 유닛 다시 설치하면 초기화가 덜 된 것 같고 배치 위치도 이상하다".
		/// </summary>
		private static void DumpPlacedUnits(string phase)
		{
			if (match == null)
				return;

			Transform stageRoot = FindStageRoot();
			int index = 0;
			foreach (ICombatant combatant in match.RegisteredCombatants)
			{
				if (combatant == null || combatant.TeamId != 0)
					continue; // 수비측만(코어/포탑/채집).

				MatchCombatant arena = combatant as MatchCombatant;
				UnitObject unit = arena != null ? arena.UnitObject : null;
				if (unit == null)
					continue;

				Vector3 world = unit.transform.position;
				Vector3 local = stageRoot != null ? stageRoot.InverseTransformPoint(world) : world;

				int animatorsEnabled = 0;
				foreach (Animator animator in unit.GetComponentsInChildren<Animator>(true))
				{
					if (animator.enabled)
						animatorsEnabled++;
				}

				int brainsEnabled = 0;
				foreach (UnitBrain brain in unit.GetComponents<UnitBrain>())
				{
					if (brain.enabled)
						brainsEnabled++;
				}

				SpriteRenderer sprite = unit.SpriteRenderer;
				Debug.Log(TAG + " UNIT[" + phase + "][" + index + "] id=" + combatant.CombatantId
					+ " hp=" + combatant.Hp + "/" + combatant.HpMax
					+ " local=" + local
					+ " scale=" + unit.transform.localScale.x.ToString("F2")
					+ " color=" + (sprite != null ? sprite.color.ToString() : "no-sprite")
					+ " sprite=" + (sprite != null && sprite.sprite != null ? sprite.sprite.name : "NULL")
					+ " animOn=" + animatorsEnabled
					+ " brainOn=" + brainsEnabled
					+ " driver=" + (unit.GetComponent<TacticDriver>() != null)
					+ " autoCast=" + (unit.SkillHandler != null ? unit.SkillHandler.AutoCastEnabled.ToString() : "no-handler")
					+ " active=" + unit.gameObject.activeInHierarchy);
				index++;
			}

			Debug.Log(TAG + " UNITS[" + phase + "] 수비 유닛 " + index + "기");
		}

		private static Vector2 WorldToScreen(Camera camera, Vector3 worldPosition)
		{
			Vector3 screenPoint = camera.WorldToScreenPoint(worldPosition);
			return new Vector2(screenPoint.x, screenPoint.y);
		}

		private static void Observe(double now)
		{
			if (match == null)
			{
				// Play 가 이미 끝났으면 매치 파괴가 아니라 *씬 통째 언로드* — 하네스 종료 사유이지 게임 결함이 아니다.
				// (둘을 구분 못 하면 환경 아티팩트를 코드 버그로 오진한다.)
				if (EditorApplication.isPlaying == false)
				{
					Debug.LogWarning(TAG + " OBSERVE-END Play 가 관찰 도중 종료됨(씬 언로드) — 관찰 "
						+ (now - observeStart).ToString("F1") + "s 시점. 게임 결함 아님, 관찰 조기 중단.");
					Finish();
					return;
				}

				// 진단 — "match 가 null" 만으론 원인 불명(컴포넌트 파괴 vs 모드 이탈 vs 싱글톤 중복 파괴).
				bool ctrlAlive = TowerDefenseModeController.TryGetExistingInstance(out TowerDefenseModeController ctrl);
				string mode = GameModeManager.TryGetExistingInstance(out GameModeManager gm) ? gm.CurrentMode.ToString() : "no-manager";
				int ctrlCount = Object.FindObjectsByType<TowerDefenseModeController>(FindObjectsInactive.Include).Length;
				int matchCount = Object.FindObjectsByType<TowerDefenseMatch>(FindObjectsInactive.Include).Length;
				Debug.LogError(TAG + " OBSERVE-FAIL match null — controllerInstance=" + ctrlAlive
					+ " controllersInScene=" + ctrlCount
					+ " matchesInScene=" + matchCount
					+ " currentMode=" + mode
					+ " ctrlGameObject=" + (ctrlAlive && ctrl != null ? ctrl.gameObject.name : "n/a"));
				Finish();
				return;
			}

			if (match.Phase != lastPhase || match.WaveIndex != lastWaveIndex)
			{
				Debug.Log(TAG + " STATE phase=" + match.Phase + " wave=" + match.WaveIndex
					+ " resource=" + match.Resource + " t=" + (now - observeStart).ToString("F1"));
				lastPhase = match.Phase;
				lastWaveIndex = match.WaveIndex;
			}

			if (match.Resource != lastResource)
			{
				if (lastResource >= 0 && match.Resource > lastResource)
					Debug.Log(TAG + " INCOME +" + (match.Resource - lastResource) + " → " + match.Resource);
				lastResource = match.Resource;
			}

			// ★ 교전이 *멈춰 있으면* = "화면엔 다 죽은 것 같은데 코어는 아직 살아있다고 센다".
			//   둘의 차이를 눈으로 못 보므로 집계 대상을 좌표·체력째로 찍는다(사용자 실증: 웨이브 2에서 멈춤).
			// ★ 실시간 전환 후 「교전이 길다」는 더 이상 신호가 아니다 — 페이즈가 없어져 *언제나* 교전 중이라
			//   이 검사가 매 판 무조건 경고를 찍었다(거짓 실패는 진짜 실패를 묻는다).
			//   실시간의 고착 신호 = **집계 수가 한참 그대로**(아무도 안 죽고 아무도 안 나온다).
			CheckStall();

			// 이름표 — 인형이 「물건」이 아니라 「아이」가 됐나. 스폰이 코루틴이라 첫 확인은 관찰 루프에서.
			if (dollsReported == false && match.DollLabels.Count > 0)
			{
				dollsReported = true;
				Debug.Log(TAG + " DOLL-NAMES count=" + match.DollLabels.Count + " first=" + match.DollLabels[0].Text);
			}

			// 영웅이 생기면 한 번 보낸다 — 스폰이 코루틴이라 「지금 없다」가 「이 판엔 없다」가 아니다.
			if (heroCommanded == false && match.HasHero)
			{
				heroCommanded = true;
				heroProbeFrom = match.HeroPosition;
				heroProbeTarget = heroProbeFrom + new Vector3(5f, 0f, 5f);
				heroProbeReady = match.CommandHero(heroProbeTarget);
				heroProbeAt = now;
				Debug.Log(TAG + " HERO commanded=" + heroProbeReady + " from=" + heroProbeFrom + " to=" + heroProbeTarget);
			}

			// 영웅이 명령한 쪽으로 실제로 가까워졌나 — 「명령을 받았다」와 「움직였다」는 다른 사실이다.
			if (heroProbeReady && now - heroProbeAt > 1.5)
			{
				heroProbeReady = false;
				float wasDistance = Vector3.Distance(heroProbeFrom, heroProbeTarget);
				float nowDistance = Vector3.Distance(match.HeroPosition, heroProbeTarget);
				if (nowDistance < wasDistance - 0.5f)
					Debug.Log(TAG + " HERO-MOVE-OK " + wasDistance.ToString("F1") + " → " + nowDistance.ToString("F1"));
				else
					Debug.LogError(TAG + " HERO-MOVE-FAIL 명령했는데 안 움직임 "
						+ wasDistance.ToString("F1") + " → " + nowDistance.ToString("F1"));
			}

			// ★ 마릿수 성능 실측 — 「수백 마리」를 원했는데 지금까지 확인된 건 수십이다. 재지 않으면
			//   늘려도 되는지 모르고, 모르면 못 늘린다. 프레임 시간과 살아있는 마릿수를 같이 찍는다.
			frameSamples++;
			frameTimeSum += Time.unscaledDeltaTime;
			if (now - lastPerfLog >= PERF_LOG_INTERVAL && frameSamples > 0)
			{
				float averageMs = frameTimeSum / frameSamples * 1000f;
				int aliveNow = match.AliveEnemyCount;
				if (aliveNow > perfPeakAlive)
					perfPeakAlive = aliveNow;

				Debug.Log(TAG + " PERF alive=" + aliveNow + " peak=" + perfPeakAlive
					+ " frameMs=" + averageMs.ToString("F1")
					+ " fps=" + (averageMs > 0f ? (1000f / averageMs).ToString("F0") : "-"));

				lastPerfLog = now;
				frameSamples = 0;
				frameTimeSum = 0f;
			}

			if (now - lastSample >= SAMPLE_INTERVAL)
			{
				lastSample = now;
				Transform stageRoot = FindStageRoot();
				int aliveEnemies = CountEnemiesNear(stageRoot);
				if (aliveEnemies > 0 && firstContactWave < 0)
				{
					firstContactWave = match.WaveIndex;
					Debug.Log(TAG + " FIRST-WAVE-SPAWNED wave=" + match.WaveIndex + " enemies=" + aliveEnemies);
				}
			}

			// 이벤트(신호)와 상태(사실)를 둘 다 본다 — 재시작이 매치를 Dispose/Begin 하며 구독이 끊기는 경로가
			// 있으면 이벤트만 믿는 검증은 "안 끝났다"고 오판한다. Outcome 이 ground truth.
			bool outcomeEnded = match.Outcome != TowerDefenseOutcome.InProgress;
			bool ended = matchEndedSeen || outcomeEnded;

			if (ended || now - observeStart > OBSERVE_SECONDS)
			{
				Debug.Log(TAG + " SUMMARY endedEvent=" + matchEndedSeen
					+ " endedOutcome=" + outcomeEnded
					+ " outcome=" + match.Outcome
					+ " wavesCleared=" + match.WaveIndex
					+ " resource=" + match.Resource
					+ " firstWaveSpawned=" + (firstContactWave >= 0)
					+ " observed=" + (now - observeStart).ToString("F1") + "s");

				// ★ 게임은 *끝나야* 게임이다. 관찰만 하고 끝내면 "결말이 오는가"를 영영 검증 못 한다
				//   (지금까지 패배를 한 번도 관측한 적이 없었다). 결말 → 배너 → 다시 도전까지 한 사이클을 닫는다.
				if (ended == false)
				{
					Debug.LogError(TAG + " CONCLUSION-FAIL 관찰 " + OBSERVE_SECONDS + "s 동안 매치가 끝나지 않음 "
						+ "— 승리도 패배도 없으면 게임이 아니라 무한 루프다. phase=" + match.Phase
						+ " coreAlive=" + (match.CoreCombatant != null && match.CoreCombatant.IsAlive));
					Finish();
					return;
				}

				restartAt = now;
				step = Step.VerifyConclusion;
			}
		}

		/// <summary>
		/// 방어 있는 교전 관측 — 격파 보상(마수 1기당 즉시 자원)이 실제로 들어오는지 본다.
		/// 「잡는 맛」은 교전 도중 자원이 오르는지로만 검증된다(웨이브 정산은 교전이 끝나야 오므로 구분됨).
		/// </summary>
		private static void ObserveDefended(double now)
		{
			const double DEFENDED_SECONDS = 55.0;

			if (match == null)
			{
				// ★ 바로 옆 관찰 경로는 이걸 구분하는데 여기만 안 했다 — Play 가 끝났으면 매치가 죽은 게
				//   아니라 *씬이 통째로 내려간* 것이다. 하네스 종료 사유이지 게임 결함이 아니다.
				//   구분 안 하면 매 판 빨간 줄이 두 개씩 쌓이고, 그 잡음이 진짜 실패를 덮는다(실제로 덮었다).
				if (EditorApplication.isPlaying == false)
					Debug.LogWarning(TAG + " DEFENDED-END Play 가 관찰 도중 종료됨(씬 언로드) — 게임 결함 아님, 관찰 조기 중단.");
				else
					Debug.LogError(TAG + " DEFENDED-FAIL Play 중인데 매치가 사라졌다.");
				Finish();
				return;
			}

			if (match.Resource != defendedLastResource)
			{
				if (defendedLastResource >= 0 && match.Resource > defendedLastResource)
				{
					int gain = match.Resource - defendedLastResource;
					bool duringAssault = match.Phase == TowerDefensePhase.Assault;
					if (duringAssault)
						killIncomeEvents++;
					Debug.Log(TAG + " GAIN +" + gain + " → " + match.Resource
						+ " phase=" + match.Phase + " aliveEnemies=" + match.AliveEnemyCount
						+ (duringAssault ? "  (교전 중 = 격파 보상)" : "  (정산)"));
				}
				defendedLastResource = match.Resource;
			}

			// ★ 첫 웨이브는 사람이 부를 때까지 안 온다(의도) — 하네스도 「사람」 역할을 해야 한다.
			//   동시에 그 관문이 진짜 걸리는지 여기서 증명한다: 기본 건설 시간(8초)을 훌쩍 넘겨도
			//   여전히 Prepare 면 시계가 안 도는 것이 맞다.
			if (match.IsWaitingForFirstCall)
			{
				if (now - defendedStart > 12.0 && firstWaveCalled == false)
				{
					firstWaveCalled = true;
					Debug.Log(TAG + " FIRST-WAVE-GATE 12초가 지나도 Prepare 유지 — 자동으로 안 넘어감 ✔ 이제 호출");
					match.RequestNextWave();
				}
				return;
			}

			DumpWaveVariety(now);

			// 고착 진단은 관찰 구간이 어디든 *같은 한 곳*이 판정한다(사본을 두면 한쪽만 고쳐진다).
			CheckStall();

			if (now - defendedStart < DEFENDED_SECONDS)
				return;

			int pierceHits = 0;
			int splashHits = 0;
			int slowApplied = 0;
			foreach (TowerDefenseWeapon weapon in Object.FindObjectsByType<TowerDefenseWeapon>(FindObjectsInactive.Include))
			{
				pierceHits += weapon.PierceHits;
				splashHits += weapon.SplashHits;
				slowApplied += weapon.SlowApplied;
			}
			Debug.Log(TAG + " TOWER-EFFECTS pierce=" + pierceHits + " splash=" + splashHits + " slow=" + slowApplied);

			TowerDefenseAdaptationState adaptation = match.Adaptation;
			Debug.Log(TAG + " ADAPT slow=" + adaptation.SlowResist.ToString("F2")
				+ " splash=" + adaptation.SplashResist.ToString("F2")
				+ " pierce=" + adaptation.PierceResist.ToString("F2")
				+ " note=\"" + TowerDefenseAdaptation.Describe(adaptation) + "\""
				+ " (상한 " + TowerDefenseAdaptation.MAX_RESIST + " — 봉인 X)");

			// 전초기지는 정수(정산에서만 나옴)로 서므로 *웨이브를 몇 번 넘긴 뒤*에 확인해야 한다.
			VerifyOutpost();

			string verdict = TAG + " DEFENDED-RESULT killIncomeEvents=" + killIncomeEvents
				+ " wave=" + match.WaveIndex + " resource=" + match.Resource
				+ " nextIncome=" + match.NextWaveIncome + " harvesters=" + match.HarvesterCount;

			if (killIncomeEvents > 0)
				Debug.Log(verdict + " → 마수를 잡을 때마다 자원이 들어온다 ✔");
			else
				Debug.LogError(verdict + " → 교전 중 보상이 한 번도 안 들어옴(격파 보상 미작동).");

			step = Step.DisarmRestart;
		}

		/// <summary>
		/// 웨이브가 뜨면 그 판의 마수를 종류째 찍는다 — 「종류가 진짜 다르게 나오는가」는 체력·덩치가
		/// 실제로 갈리는지로만 확인된다(색만 다르고 스탯이 같으면 종류는 착시다).
		/// </summary>
		private static void DumpWaveVariety(double now)
		{
			if (match == null || match.Phase != TowerDefensePhase.Assault)
				return;

			if (match.WaveIndex != lastDumpedWave)
			{
				lastDumpedWave = match.WaveIndex;
				waveDumpAt = now + 1.5; // 스폰 코루틴이 끝날 시간을 준다.
				return;
			}

			if (waveDumpAt < 0.0 || now < waveDumpAt)
				return;
			waveDumpAt = -1.0;

			int index = 0;
			foreach (ICombatant combatant in match.WaveEnemies)
			{
				if (combatant == null)
					continue;
				Transform enemyTransform = ((MonoBehaviour)combatant).transform;
				Debug.Log(TAG + " VARIETY wave=" + match.WaveIndex + " [" + index + "]"
					+ " hp=" + combatant.Hp + "/" + combatant.HpMax
					+ " scale=" + enemyTransform.localScale.x.ToString("F2")
					+ " alive=" + combatant.IsAlive);
				index++;
			}
		}

		/// <summary>
		/// 정수 — 바깥 노드 채집이 정수를 내고, 강화(연구·승급)가 자원이 아니라 정수를 쓰는가.
		/// 「멀리 나가야 강해진다」가 두 통장으로 성립하는지 본다.
		/// </summary>
		private static void VerifyEssence(string when = "배치 직후")
		{
			Transform stageRoot = FindStageRoot();
			if (match == null || stageRoot == null)
				return;

			// 배치는 이미 DoPlacements 가 했다(코루틴이 끝날 시간을 벌기 위해) — 여기선 결과만 읽는다.
			// ★ 읽기 전에 판에게 *다시 세어보라*고 시킨다. 안 시켰더니 발전 인형을 세운 지 0.85초 만에
			//   읽어 「전기가 안 닿음」이라 찍혔는데, 라이브로 물어보니 전기는 닿아 있었다(정수도 나고 있었다).
			//   기다리는 시간을 늘려 해결하려다 오히려 순서가 깨졌다 — 시계와 싸우는 대신 결정적으로 만든다.
			match.RefreshSupplyForVerification();
			string verdict = TAG + " ESSENCE[" + when + "] harvesters=" + match.HarvesterCount
				+ " outer=" + match.OuterHarvesters + "/판의바깥광맥=" + match.OuterNodeCount
				+ " outerSupplied=" + match.SuppliedOuterHarvesters
				+ " outerPowered=" + match.PoweredOuterHarvesters
				+ " nextIncome=" + match.NextWaveIncome
				+ " nextEssence=" + match.NextWaveEssence
				+ " essence=" + match.Essence;

			// ★ 세 원인을 갈라 말한다 — 안 갈라 말하면 「바깥 노드인데 안 나온다」는 *거짓 실패*가 찍힌다
			//   (실측: 실제로는 바깥에 세운 적이 없거나, 세웠어도 사슬이 안 닿아 있었다).
			if (match.HarvesterCount == 0)
				Debug.Log(verdict + " → 채집을 못 세움(자원 부족/자리 없음) — 확인 못 함");
			else if (match.OuterNodeCount == 0)
				Debug.LogError(verdict + " → 이 판에 바깥 광맥이 아예 없다 — 정수를 낼 자리가 없으니 「멀리 나가야 강해진다」 축이 통째로 죽는다.");
			else if (match.OuterHarvesters == 0)
				Debug.Log(verdict + " → 바깥 광맥은 있는데 거기 못 세움 — 정수 0 은 정상, 확인 못 함");
			else if (match.SuppliedOuterHarvesters == 0)
				Debug.Log(verdict + " → 바깥에 세웠지만 사슬이 안 닿음 — 정수 0 은 규칙대로, 확인 못 함");
			else if (match.PoweredOuterHarvesters == 0)
				Debug.Log(verdict + " → 이어졌지만 전기가 안 닿음 — 정수 0 은 규칙대로, 확인 못 함");
			else if (match.NextWaveEssence > 0)
				Debug.Log(verdict + " → 이어진 바깥 채집이 정수를 낸다 ✔");
			else
				Debug.LogError(verdict + " → 이어진 바깥 채집이 있는데 정수가 안 나온다.");
		}

		/// <summary> 전초기지 — 정수로 서고, 마수의 목표(유출 지점)가 하나 느는가. </summary>
		private static void VerifyOutpost()
		{
			Transform stageRoot = FindStageRoot();
			if (match == null || stageRoot == null)
				return;

			if (match.Essence < match.Stage.OutpostEssenceCost)
			{
				Debug.Log(TAG + " OUTPOST-SKIP 정수 부족(" + match.Essence + "/" + match.Stage.OutpostEssenceCost
					+ ") — 첫 정산 전에는 못 세움(의도된 설계)");
				return;
			}

			int before = match.OutpostCount;
			foreach (Vector3 local in FindPlaceableSpots(stageRoot, 1))
			{
				match.TryPlaceOutpost(stageRoot.TransformPoint(local));
				break;
			}

			string verdict = TAG + " OUTPOST count " + before + " → " + match.OutpostCount
				+ " essence=" + match.Essence + " supplied=" + match.SuppliedBuildings;
			if (match.OutpostCount > before)
				Debug.Log(verdict + " → 지킬 곳이 하나 늘었다 ✔");
			else
				Debug.LogError(verdict + " → 전초기지가 안 선다.");
		}

		/// <summary> 보급 — 코어에서 이어진 건물이 잡히고, 끊긴 채집이 수입에서 빠지는가. </summary>
		private static void VerifySupply()
		{
			Transform stageRoot = FindStageRoot();
			if (match == null || stageRoot == null)
				return;

			// ★ 확인할 것은 「가까운 건 이어지고 먼 건 안 이어진다」이다. 앞 단계(판매)가 코어 근처 건물을
			//   치워버려 사슬의 시작점이 사라졌으므로, 코어 옆에 하나 세워 대조군을 만든다.
			foreach (Vector3 local in FindPlaceableSpots(stageRoot, 1))
			{
				match.TryPlaceWall(stageRoot.TransformPoint(local));
				break;
			}

			string verdict = TAG + " SUPPLY buildings=" + match.SupplyBuildingCount
				+ " reach=" + match.Stage.SupplyReach
				+ " supplied=" + match.SuppliedBuildings
				+ " disconnected=" + match.DisconnectedHarvesters
				+ " nextIncome=" + match.NextWaveIncome
				+ " nextEssence=" + match.NextWaveEssence;

			if (match.SuppliedBuildings > 0)
				Debug.Log(verdict + " → 코어에서 사슬이 이어진다 ✔");
			else
				Debug.LogError(verdict + " → 아무 건물도 보급에 안 잡힌다(사슬 계산 실패).");
		}

		/// <summary> 이벤트 웨이브 — 웨이브마다 성격이 붙고, 마리수가 실제로 달라지는가(예고와 스폰이 같은 함수). </summary>
		private static void VerifyWaveEvents()
		{
			if (match == null)
				return;

			System.Text.StringBuilder line = new();
			int eventWaves = 0;
			int countVaried = 0;
			int plainCount = match.ScaledEnemyCount(0);

			for (int wave = 0; wave < 9; wave++)
			{
				TowerDefenseWaveEventKind kind = match.WaveEventAt(wave);
				int count = match.ScaledEnemyCount(wave);
				if (kind != TowerDefenseWaveEventKind.None)
				{
					eventWaves++;
					if (count != match.Stage.Rules.EnemiesInWave(wave))
						countVaried++;
				}
				line.Append(wave).Append(':').Append(TowerDefenseWaveEvent.DisplayName(kind) is { Length: > 0 } name ? name : "-")
					.Append('(').Append(count).Append(") ");
			}

			string verdict = TAG + " WAVE-EVENTS " + line.ToString().TrimEnd()
				+ " | eventWaves=" + eventWaves + " countVaried=" + countVaried + " plain0=" + plainCount;
			if (eventWaves >= 2 && countVaried >= 1)
				Debug.Log(verdict + " → 웨이브마다 성격이 바뀐다 ✔");
			else
				Debug.LogError(verdict + " → 성격이 안 붙거나 마리수가 안 변한다.");
		}

		/// <summary>
		/// 연구 — 코어를 골라 정수로 한 단계 올린다. 자원이 아니라 *정수*로 사므로 앞의 항목들과 지갑이 달라
		/// 순서를 다툴 일이 없다. 정수가 모자라면 「확인 못 함」으로 남긴다(가짜 실패 X).
		///
		/// ★ 예전엔 이 확인이 「연구 인형을 짓는다」였는데, 그 건물은 핫바에서 사라진 뒤로 *플레이어가
		///   갈 수 없는 길*이 되어 있었다 — 그런데도 확인은 초록불을 켰다(핫바를 우회해 직접 불렀으니까).
		///   아무도 못 가는 길에 켜지는 초록불은 없느니만 못하다. 살아있는 길로 옮긴다.
		/// </summary>
		/// <summary>
		/// 갈래 하나를 찍고 *판에서 읽히는 값*이 실제로 오르는지 잰다.
		/// 통과해도 전후 숫자를 남긴다 — 「올랐다」만 적으면 얼마나 올랐는지 사람이 못 본다.
		/// </summary>
		private static void VerifyResearchEffect(string label, TowerDefenseResearchEffect effect,
			System.Func<float> read)
		{
			float before = read();
			bool taken = match.TryTakeResearchNode(effect, 0.25f, cost: 0, usesEssence: false);
			float after = read();
			Debug.Log(TAG + " RESEARCH-NODE " + label + " accepted=" + taken
				+ " " + before.ToString("F2") + " → " + after.ToString("F2"));
			if (taken == false)
				Debug.LogError(TAG + " RESEARCH-FAIL " + label + " 마디를 못 찍었다.");
			else if (after <= before)
				Debug.LogError(TAG + " RESEARCH-FAIL " + label + " 갈래가 하는 일이 없다 — 찍어도 판이 그대로다.");
		}

		private static void VerifyResearch()
		{
			if (match == null)
				return;

			// ★ 플레이어가 실제로 가는 길로 확인한다. 예전엔 옛 연구(단추 한 번에 한 단계)를 불렀는데,
			//   그 길은 이제 화면 어디에서도 안 열린다 — **아무도 못 가는 길을 확인하고 초록불**을 켜고
			//   있었다. 확인 도구가 실물과 다른 길을 밟으면 그 뒤로 무엇을 확인해도 못 믿는다.
			//   지금 연구 = 성좌의 마디를 찍는 것. 값은 0 으로 부른다(정수 유무에 흔들리지 않게 —
			//   값을 치르는 길은 규칙층 시험이 따로 잠갔다).
			float damageBefore = match.TowerDamageMultiplier;
			bool damageTaken = match.TryTakeResearchNode(TowerDefenseResearchEffect.TowerDamage, 0.25f, cost: 0, usesEssence: false);
			Debug.Log(TAG + " RESEARCH-NODE 피해 accepted=" + damageTaken
				+ " damageMultiplier " + damageBefore.ToString("F2") + " → " + match.TowerDamageMultiplier.ToString("F2"));
			if (damageTaken == false)
				Debug.LogError(TAG + " RESEARCH-FAIL 마디를 못 찍었다 — 성좌에서 아무것도 못 고른다는 뜻이다.");
			else if (match.TowerDamageMultiplier <= damageBefore)
				Debug.LogError(TAG + " RESEARCH-FAIL 찍었는데 포탑이 안 세졌다 — 연구가 하는 일이 없다.");

			// ★ 여섯 갈래를 *전부* 잰다. 예전엔 둘만 봤는데, 안 보는 갈래는 조용히 죽어도 아무도 모른다
			//   (실제로 오늘 카드 셋이 「뽑히는데 효과 0」인 채로 살아 있었다).
			//   각 갈래마다 *판에서 실제로 읽히는 값*을 전후로 찍는다 — 통과해도 숫자를 남겨야
			//   「무엇이 얼마나 세졌나」를 사람이 눈으로 확인할 수 있다.
			// ★ 배수가 아니라 *화면이 그리는 원*으로 잰다 — 배수만 재면 「총은 멀리 나가는데 원은 그대로」를
			//   못 본다(실제로 그랬다). 원이 거짓말하면 배치 판단의 근거가 통째로 사라진다.
			VerifyResearchEffect("사거리", TowerDefenseResearchEffect.TowerRange,
				() => match.TowerRange());
			VerifyResearchEffect("보급 거리", TowerDefenseResearchEffect.SupplyReach,
				() => match.EffectiveSupplyReach);
			// 규칙이 아니라 *화면에 그려진 원*도 같이 잰다 — 사거리에서 겪은 그 병(총만 멀리 나감)이
			// 보급에도 그대로 있었다. 규칙만 재면 원이 굳어도 초록불이 켜진다.
			Debug.Log(TAG + " SUPPLY-RING 그려진 원 " + match.DrawnSupplyReach.ToString("F2")
				+ " · 규칙 " + match.EffectiveSupplyReach.ToString("F2"));
			if (match.DrawnSupplyReach > 0f
				&& Mathf.Approximately(match.DrawnSupplyReach, match.EffectiveSupplyReach) == false)
			{
				Debug.LogError(TAG + " SUPPLY-RING-FAIL 원과 실제 보급 거리가 다르다 — 원이 거짓말한다.");
			}
			VerifyResearchEffect("채집 수입", TowerDefenseResearchEffect.HarvestYield,
				() => match.NextWaveIncome);
			VerifyResearchEffect("코어 방어", TowerDefenseResearchEffect.CoreArmor,
				() => match.CoreCombatant != null && match.CoreCombatant.UnitObject != null
					? match.CoreCombatant.UnitObject.UnitStat[UnitStatType.HP_MAX] : 0f);
			// 영웅은 판에 영웅이 서 있어야 값이 읽힌다 — 누적 비율로 확인한다(그것마저 안 오르면 배선이 끊긴 것).
			VerifyResearchEffect("영웅", TowerDefenseResearchEffect.HeroPower,
				() => match.ResearchBonus(TowerDefenseResearchEffect.HeroPower));

			// 큰 마디 = 연구 한 단계 = 새 칸 해금. 이 고리가 끊기면 성좌를 다 뚫어도 지을 것이 그대로다.
			int levelBefore = match.ResearchLevel;
			int slotsBefore = match.AvailableSlots.Count;
			match.GrantResearchLevel();
			Debug.Log(TAG + " RESEARCH-LEVEL " + levelBefore + " → " + match.ResearchLevel
				+ " 칸 " + slotsBefore + " → " + match.AvailableSlots.Count);
			if (match.ResearchLevel <= levelBefore)
				Debug.LogError(TAG + " RESEARCH-FAIL 큰 마디를 뚫었는데 단계가 안 올랐다.");

			VerifyResearchRestoreWithoutPanel();
			VerifyResearchPanel();
			// ★ *포탑이 선 뒤*에 잰다 — 채집만 세운 시점에 재봤더니 사거리 원이 아예 0개라
			//   「0개 중 0개 어긋남」이라는 헛초록불이 켜졌다. 안 돈 검사는 검사가 아니다.
			VerifyRingMeaning();
		}

		/// <summary>
		/// 세워진 물건의 원이 *그 물건이 뜻하는 것*을 그리는가.
		///
		/// ★ 사거리 원은 「이만큼 쏜다」는 뜻이다 — 쏘지 않는 물건에 뜨면 그 자체로 거짓말이다.
		///   지금은 세우는 쪽이 채집·발전을 걸러내고 있어서 성립하는데, 그 가드가 사라지면 조용히 깨진다.
		/// ★ 하나도 못 쟀으면 실패로 본다 — 안 돈 검사는 통과가 아니다(실제로 그 헛초록불을 봤다).
		/// </summary>
		private static void VerifyRingMeaning()
		{
			// ★ 꺼져 있는 것도 센다 — 사거리 원은 물어볼 때만 보이므로 평소엔 숨어 있다.
			//   숨은 것을 안 세면 「원이 하나도 없다」는 거짓 진단이 나온다.
			TowerDefenseRing[] rings = Object.FindObjectsByType<TowerDefenseRing>(FindObjectsInactive.Include);
			int total = 0;
			int wrong = 0;
			foreach (TowerDefenseRing ring in rings)
			{
				if (ring == null || ring.name != "RangeRing")
					continue;

				total++;
				Transform owner = ring.transform.parent;
				if (owner != null && owner.GetComponent<TowerDefenseWeapon>() != null)
					continue;

				wrong++;
				Debug.LogError(TAG + " RING-MEANING-FAIL " + (owner != null ? owner.name : "?")
					+ " 에 사거리 원이 " + ring.Radius.ToString("F2") + " 로 떠 있는데 쏘는 물건이 아니다.");
			}

			// 이름별로 남긴다 — 0 이 나왔을 때 「원이 없다」인지 「이름이 다르다」인지 바로 갈린다.
			string names = "";
			foreach (TowerDefenseRing ring in rings)
			{
				if (ring != null)
					names += ring.name + " ";
			}

			Debug.Log(TAG + " RING-MEANING 사거리 원 " + total + "개 · 쏘지 않는데 뜬 것 " + wrong
				+ "개 · 판 위의 모든 원 [" + (names == "" ? "없음" : names.Trim()) + "]");
			if (total == 0)
				Debug.LogError(TAG + " RING-MEANING-FAIL 잴 것이 하나도 없었다 — 검사가 헛돈 것이지 통과가 아니다.");
		}

		/// <summary>
		/// 성좌 *화면* — 열리는가 · 전체화면인가 · 마디가 그려지는가 · 열면 판이 멈추고 닫으면 도는가.
		///
		/// ★ 이걸 재는 이유: 지금까지 성좌는 규칙층만 두드려 검사했고 **화면은 한 번도 안 열어봤다**.
		///   「전체화면으로」와 「그래프식으로」는 사용자가 직접 요청한 것인데, 그게 지켜지는지 말해주는
		///   기계가 하나도 없었다 — 안 재는 것은 조용히 죽는다.
		/// </summary>
		private static void VerifyResearchPanel()
		{
			TowerDefenseModeController controller = TowerDefenseModeController.Instance;
			if (controller == null || match == null)
			{
				Debug.LogError(TAG + " RESEARCH-PANEL-FAIL 판 진행자를 못 찾았다.");
				return;
			}

			bool pausedBefore = match.IsPaused;
			controller.OpenResearchPanel();

			if (controller.IsResearchOpen == false)
			{
				Debug.LogError(TAG + " RESEARCH-PANEL-FAIL 성좌가 안 열린다 — 사람도 못 연다는 뜻이다.");
				return;
			}

			Debug.Log(TAG + " RESEARCH-PANEL 열림 · 마디 " + controller.ResearchNodeCount
				+ "개 · 판 멈춤 " + match.IsPaused);
			if (controller.ResearchNodeCount <= 1)
				Debug.LogError(TAG + " RESEARCH-PANEL-FAIL 마디가 없다 — 그래프가 아니라 빈 판이다.");
			if (match.IsPaused == false)
				Debug.LogError(TAG + " RESEARCH-PANEL-FAIL 성좌가 화면을 덮었는데 판이 계속 돈다 — 그 사이 코어가 털린다.");

			// ★ 크기는 *다음 틱*에 잰다 — 연 그 프레임엔 자리가 아직 안 잡혀 NaN 이다(실측).
			//   재시작 단계가 바로 다음 틱에 돌므로 거기서 재고 닫는다(성좌를 연 채로 오래 두지 않는다).
			researchPanelPausedBefore = pausedBefore;
			researchPanelMeasurePending = true;
		}

		private static bool researchPanelPausedBefore;
		private static bool researchPanelMeasurePending;

		/// <summary> 열어둔 성좌를 *한 틱 뒤에* 재고 닫는다 — 「전체화면으로」가 지켜지는지는 이 숫자뿐이다. </summary>
		private static void MeasureAndCloseResearchPanel()
		{
			if (researchPanelMeasurePending == false)
				return;
			researchPanelMeasurePending = false;

			TowerDefenseModeController controller = TowerDefenseModeController.Instance;
			if (controller == null || match == null)
				return;

			// ★ 화면 픽셀과 견주면 안 된다 — UI 는 자기 좌표계(논리 픽셀)로 잰다. 배율이 1 이 아니면
			//   둘의 단위가 달라 「덮는 비율 55%」 같은 헛수가 나온다(실측: 배율을 Expand 로 바꾼 직후
			//   멀쩡한 전체화면이 실패로 찍혔다). *같은 좌표계에 있는 UI 뿌리*와 견준다.
			Rect panel = controller.ResearchScreenRect;
			Rect host = controller.UiRootRect;
			float hostArea = Mathf.Max(1f, host.width * host.height);
			float coverage = (panel.width * panel.height) / hostArea;
			Debug.Log(TAG + " RESEARCH-PANEL 덮는 비율 " + coverage.ToString("P0")
				+ " (" + panel.width.ToString("F0") + "x" + panel.height.ToString("F0")
				+ " / UI 뿌리 " + host.width.ToString("F0") + "x" + host.height.ToString("F0")
				+ " · 화면 " + Screen.width + "x" + Screen.height + ")");
			// 한 틱 뒤에 재는데도 NaN 이면 그건 「아직」이 아니라 자리가 영영 안 잡힌 것 — 실패다.
			if (float.IsNaN(coverage))
				Debug.LogError(TAG + " RESEARCH-PANEL-FAIL 한 틱 뒤에도 자리가 안 잡혔다 — 크기를 잴 수 없다.");
			else if (coverage < 0.9f)
				Debug.LogError(TAG + " RESEARCH-PANEL-FAIL 전체화면이 아니다 — 요청은 화면을 통째로 덮는 것이었다.");

			controller.CloseOverlays();
			Debug.Log(TAG + " RESEARCH-PANEL 닫음 · 열림 " + controller.IsResearchOpen
				+ " · 판 멈춤 " + match.IsPaused + "(열기 전 " + researchPanelPausedBefore + ")");
			if (controller.IsResearchOpen)
				Debug.LogError(TAG + " RESEARCH-PANEL-FAIL 성좌가 안 닫힌다.");
			if (match.IsPaused != researchPanelPausedBefore)
				Debug.LogError(TAG + " RESEARCH-PANEL-FAIL 닫았는데 멈춤 상태가 원래대로 안 돌아온다.");
		}

		/// <summary>
		/// 이어하기 — 성좌 화면을 *한 번도 안 연* 채로 저장을 되돌려도 연구가 살아 있는가.
		///
		/// ★ 이걸 재는 이유: 되돌리는 일을 성좌 화면이 들고 있으면, 화면은 사람이 처음 열 때 세워지는데
		///   이어하기는 그보다 먼저 일어난다 → 되돌릴 곳이 없어 저장에 적힌 연구가 통째로 조용히 사라진다.
		///   실제로 그랬다. 규칙이 화면 유무와 무관한지는 「화면 없이」 재야만 드러난다.
		/// </summary>
		private static void VerifyResearchRestoreWithoutPanel()
		{
			TowerDefenseModeController controller = TowerDefenseModeController.Instance;
			if (controller == null)
			{
				Debug.LogError(TAG + " RESEARCH-RESTORE-FAIL 판 진행자를 못 찾았다.");
				return;
			}

			// ★ 사람이 찍는 것과 *같은 문*으로 찍는다 — 규칙층을 직접 두드리면 저장에 안 적히는
			//   병(방금 그것)을 검사기가 못 본다.
			// 값이 없으면 사람도 못 찍는다 — 찍는 일 자체가 목적이 아니므로 넉넉히 채워두고 시작한다.
			match.GrantForVerification(0, 99);
			if (controller.TryGetFirstResearchNodeId(out int firstNode) == false)
			{
				Debug.LogError(TAG + " RESEARCH-RESTORE-FAIL 코어에서 이어지는 마디가 하나도 없다 — 성좌가 안 세워졌다.");
				return;
			}

			if (controller.ChooseResearchNode(firstNode) == false)
			{
				Debug.LogError(TAG + " RESEARCH-RESTORE-FAIL 첫 마디를 못 찍었다(값 " + match.Essence + ") — 사람도 못 찍는다.");
				return;
			}

			List<int> saved = new List<int>();
			match.CollectResearchInto(saved);
			if (saved.Count == 0)
			{
				Debug.LogError(TAG + " RESEARCH-RESTORE-FAIL 찍었는데 저장에 적힐 마디가 0개다.");
				return;
			}

			// ★ 한 갈래만 재면 안 된다 — 되돌린 마디가 *다른* 갈래면 그 갈래는 그대로라 거짓 실패가 난다
			//   (실제로 처음 그렇게 재서 멀쩡한 고침을 실패로 읽었다). 갈래 전부의 합으로 잰다.
			float before = TotalResearchBonus();
			match.ClearResearch();
			float cleared = TotalResearchBonus();
			match.RestoreResearchFrom(saved);
			float after = TotalResearchBonus();

			Debug.Log(TAG + " RESEARCH-RESTORE 마디 " + saved.Count + "개 · 갈래 합 "
				+ before.ToString("F2") + " → 지움 " + cleared.ToString("F2") + " → 되돌림 " + after.ToString("F2"));
			if (Mathf.Approximately(cleared, 0f) == false)
				Debug.LogError(TAG + " RESEARCH-RESTORE-FAIL 새 판인데 지난 판 연구가 남아 있다.");
			if (after <= cleared)
				Debug.LogError(TAG + " RESEARCH-RESTORE-FAIL 이어하기가 연구를 못 되돌렸다 — 저장은 적혔는데 판이 안 받는다.");
		}

		/// <summary> 갈래 전부의 누적 합 — 어느 갈래가 되돌아왔든 잡힌다. </summary>
		private static float TotalResearchBonus()
		{
			float total = 0f;
			foreach (TowerDefenseResearchEffect effect in System.Enum.GetValues(typeof(TowerDefenseResearchEffect)))
				total += match.ResearchBonus(effect);
			return total;
		}

		/// <summary> 승급 — 같은 자리에 같은 종류를 다시 지으면 단계가 오르고 사거리·피해가 자라는가. </summary>
		/// <summary>
		/// 정수가 모자랄 때 화면이 **버는 법까지** 말하는가.
		///
		/// ★ 사용자가 직접 물은 것이다: "정수 어떻게 얻어? 강화를 할 수가 없는데?" 화면이 「부족」만
		///   말하면 사람은 거기서 막힌다 — 모자란 건 이미 아는 사실이고, 필요한 건 *다음 행동*이다.
		/// ★ 승급은 아예 조용히 실패하고 있었다(눌러도 아무 말이 없다 = 고장으로 읽힌다).
		/// </summary>
		private static void VerifyEssenceShortageTalks()
		{
			if (match == null)
				return;

			// 정수를 바닥내고 정수로 사는 것을 눌러 본다 — 거절 문구가 나와야 한다.
			int essence = match.Essence;
			if (essence > 0)
				match.SpendEssenceForVerification(essence);

			int before = match.Essence;
			bool outpostRejected = match.TryPlaceOutpost(FindStageRoot() != null
				? FindStageRoot().TransformPoint(new Vector3(6f, 0f, 6f))
				: Vector3.zero) == false;

			Debug.Log($"{TAG} 정수 안내 — 정수 {before} · 전초기지 거절 {outpostRejected}");

			if (outpostRejected == false)
			{
				Debug.Log(TAG + " 정수 안내 — 못 쟀다(정수 0 인데도 지어졌다면 값이 0 인 스테이지다).");
				return;
			}

			// 마지막 거절 문구를 매치가 들고 있어야 화면이 무엇을 말했는지 잴 수 있다.
			string said = match.LastRejectReason;
			Debug.Log($"{TAG} 정수 안내 — 화면이 한 말: 「{said}」");

			if (string.IsNullOrEmpty(said) || said.Contains("정수 부족") == false)
				Debug.LogError(TAG + " 정수 안내 FAIL — 정수가 모자란데 그렇게 말하지 않는다.");
			else if (said.Contains("채집") == false || said.Contains("둥지") == false || said.Contains("서식지") == false)
				Debug.LogError(TAG + " 정수 안내 FAIL — 「부족」만 말하고 *버는 법*을 안 말한다: 「" + said + "」");
		}

		private static void VerifyUpgrade()
		{
			Transform stageRoot = FindStageRoot();
			if (sellProbeReady == false || match == null || stageRoot == null)
				return;

			// ★ 새로 짓지 않고 *이미 세운* 포탑을 올린다 — 확인하려는 건 「같은 자리에 다시 지으면 자라는가」이지
			//   「지을 돈이 있는가」가 아니다. 새로 지으면 그 값이 승급 예산을 먹어 기능이 아니라 잔고를 검사하게 된다.
			Vector3 world = stageRoot.TransformPoint(sellProbeLocal);

			// ★ 승급은 정수(강화 전용 재화)를 쓴다 — 정수는 웨이브 정산에서만 나오므로 첫 웨이브 전에는 못 올린다.
			//   이건 의도된 설계(강화는 개척의 결과)라, 없으면 「확인 못 함」이지 실패가 아니다.
			if (match.Essence <= 0)
			{
				Debug.Log(TAG + " UPGRADE-SKIP 정수 0 — 첫 웨이브 정산 전에는 승급 불가(의도된 설계)");
				return;
			}

			int before = match.Essence;
			bool upgraded = match.TryPlaceTower(world, sellProbeSlot); // 세운 그 종류로 걸어야 승급 검사가 된다.

			int level = -1;
			foreach (TowerDefenseWeapon weapon in Object.FindObjectsByType<TowerDefenseWeapon>(FindObjectsInactive.Include))
			{
				if (weapon != null && weapon.Level > level)
					level = weapon.Level;
			}

			string verdict = TAG + " UPGRADE ok=" + upgraded + " slot=" + sellProbeSlot + " maxLevel=" + level
				+ " essence " + before + " → " + match.Essence;
			if (upgraded && level >= 2 && match.Essence < before)
				Debug.Log(verdict + " → 같은 자리에 다시 지으면 자란다 ✔");
			else
				Debug.LogError(verdict + " → 승급이 안 되거나 값을 안 치른다.");
		}

		/// <summary>
		/// 확인 항목에 *그 항목이 쓸 몫*을 채워준다 — 앞선 배치가 예산을 다 쓰면 뒤의 확인이 전부
		/// 「돈이 없어 못 함」이 되고, 하네스는 그걸 「기능이 고장났다」고 적는다(실측: 함정 24/25 로 실패).
		/// ★ 값을 채우는 것은 *배치 규칙을 우회하지 않는다* — 자리·보급·암반 검사는 그대로 통과해야 한다.
		/// </summary>
		private static void EnsureBudget(int needed)
		{
			if (match != null && match.Resource < needed)
				match.GrantForVerification(needed - match.Resource, 0);
		}

		/// <summary> 함정 — 깔리는가(길목에 소모품을 놓는 수단이 실제로 존재하는가). </summary>
		private static void VerifyTrap()
		{
			Transform stageRoot = FindStageRoot();
			if (match == null || stageRoot == null)
				return;

			EnsureBudget(match.Stage.TrapCost * 2);
			int placed = 0;
			int before = match.Resource;
			foreach (Vector3 local in FindPlaceableSpots(stageRoot, 2))
			{
				if (match.TryPlaceTrap(stageRoot.TransformPoint(local)))
					placed++;
			}

			string verdict = TAG + " TRAP placed=" + placed + " resource " + before + " → " + match.Resource;
			if (placed > 0 && match.Resource < before)
				Debug.Log(verdict + " ✔");
			else
				Debug.LogError(verdict + " → 함정이 안 깔리거나 값을 안 치른다.");
		}

		/// <summary>
		/// 벽 — 세워지는가, 그리고 *길을 완전히 막는 자리는 거절되는가*.
		/// 후자가 핵심 불변식이다(막히면 마수가 굳어 웨이브가 영영 안 끝난다).
		/// 코어를 빙 둘러 벽으로 감싸보고 마지막 한 칸이 거절되는지로 확인한다.
		/// </summary>
		private static void VerifyWall()
		{
			Transform stageRoot = FindStageRoot();
			if (match == null || stageRoot == null)
				return;

			// ★ 예산을 먼저 채운다 — 돈이 없어서 거절된 것을 「길을 끊어서 거절됐다」로 읽으면
			//   이 검사는 *거짓으로 통과*한다(잔고가 불변식을 대신 증명해 버린다).
			EnsureBudget(match.Stage.WallCost * 12);

			// ① 평범한 자리에 한 장 — 서야 한다.
			int placed = 0;
			foreach (Vector3 local in FindPlaceableSpots(stageRoot, 3))
			{
				if (match.TryPlaceWall(stageRoot.TransformPoint(local)))
					placed++;
			}

			// ② 코어를 완전히 감싸본다 — 마지막에 반드시 막혀야(거절돼야) 한다.
			int accepted = 0;
			int rejected = 0;
			for (int offsetX = -1; offsetX <= 1; offsetX++)
			{
				for (int offsetY = -1; offsetY <= 1; offsetY++)
				{
					if (offsetX == 0 && offsetY == 0)
						continue;
					Vector3 local = new Vector3(offsetX + 0.5f, 0f, offsetY + 0.5f);
					if (match.TryPlaceWall(stageRoot.TransformPoint(local)))
						accepted++;
					else
						rejected++;
				}
			}

			string verdict = TAG + " WALL placed=" + placed + " ringAccepted=" + accepted + " ringRejected=" + rejected;
			if (placed > 0 && rejected > 0)
				Debug.Log(verdict + " → 벽은 서고, 길을 끊는 자리는 거절된다 ✔");
			else
				Debug.LogError(verdict + " → 벽이 안 서거나(placed=0) 코어를 완전히 봉인할 수 있다(rejected=0).");
		}

		/// <summary> 판매 — 세운 것을 팔면 자원이 돌아오고 자리가 비는가(정착 후에 확인). </summary>
		private static void VerifySell()
		{
			Transform stageRoot = FindStageRoot();
			if (sellProbeReady == false || match == null || stageRoot == null)
				return;

			Vector3 world = stageRoot.TransformPoint(sellProbeLocal);
			int before = match.Resource;
			bool sold = match.TrySell(world, match.Stage.SellRefundRatio);
			bool freed = match.IsCellOccupied(world) == false;

			string verdict = TAG + " SELL ok=" + sold + " resource " + before + " → " + match.Resource
				+ " soldValue=" + match.LastSoldValue + " ratio=" + match.Stage.SellRefundRatio.ToString("F2")
				+ " refund=" + match.LastSellRefund + " cellFreed=" + freed;
			if (sold && match.Resource > before && freed)
				Debug.Log(verdict + " → 되돌릴 수 있다 ✔");
			else
				Debug.LogError(verdict + " → 판매가 안 되거나 자리가 안 비었다.");
		}

		/// <summary>
		/// UI 위 클릭이 설치로 새지 않는지 — 「HUD 버튼을 눌렀는데 그 아래 지면에 건물이 선다」 회귀 방지.
		/// 하네스는 실제 마우스를 못 누르므로 *판정 함수*를 진실의 기준으로 검사한다:
		/// ① HUD 버튼이 차지한 화면 좌표에서 UI 위라고 답하는가 ② 빈 지면 좌표에선 아니라고 답하는가.
		/// 버튼의 화면 좌표는 변환식을 역산하지 않고 화면을 성기게 훑어 구한다(같은 식을 두 번 쓰면
		/// 자기 자신을 검증하는 꼴이라 의미가 없다).
		/// </summary>
		private static void VerifyUiPointerGuard()
		{
			UIRoot uiRoot = Object.FindAnyObjectByType<UIRoot>();
			VisualElement hud = uiRoot != null && uiRoot.ModeHudLayer != null
				? uiRoot.ModeHudLayer.Q(nameof(TowerDefenseHudView))
				: null;
			Button button = hud != null ? hud.Q<Button>() : null;
			if (button == null)
			{
				Debug.LogError(TAG + " UIGUARD-FAIL HUD 버튼을 못 찾음 — 판정 검사 불가.");
				return;
			}

			Rect buttonPanelRect = button.worldBound;
			const int SAMPLE_COLUMNS = 96;
			const int SAMPLE_ROWS = 54;
			Vector2 buttonScreenPoint = new Vector2(-1f, -1f);

			for (int column = 0; column <= SAMPLE_COLUMNS && buttonScreenPoint.x < 0f; column++)
			{
				for (int row = 0; row <= SAMPLE_ROWS; row++)
				{
					Vector2 candidate = new Vector2(
						Screen.width * column / (float)SAMPLE_COLUMNS,
						Screen.height * row / (float)SAMPLE_ROWS);
					Vector2 panelPoint = RuntimePanelUtils.ScreenToPanel(
						uiRoot.Root.panel, new Vector2(candidate.x, Screen.height - candidate.y));
					if (buttonPanelRect.Contains(panelPoint))
					{
						buttonScreenPoint = candidate;
						break;
					}
				}
			}

			if (buttonScreenPoint.x < 0f)
			{
				Debug.LogError(TAG + " UIGUARD-FAIL 버튼의 화면 좌표를 못 찾음 buttonRect=" + buttonPanelRect);
				return;
			}

			bool overButton = UIPointer.IsOverInteractive(buttonScreenPoint);
			// 화면 정중앙 = 개척지 한복판. HUD 는 모서리에 있으므로 여기는 반드시 설치 가능해야 한다.
			bool overGround = UIPointer.IsOverInteractive(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));

			string verdict = TAG + " UIGUARD button=" + overButton + " ground=" + overGround
				+ " buttonScreen=" + buttonScreenPoint + " buttonText=" + button.text;

			if (overButton && overGround == false)
				Debug.Log(verdict + " → UI 위는 막고 지면은 통과 ✔");
			else
				Debug.LogError(verdict + " → UI 클릭이 설치로 새거나(button=False) 지면이 막힌다(ground=True).");
		}

		// 화면 어디에 무엇이 놓였나 — 겹치면 안 되는 덩어리들. 전면(배너·드래프트)은 *덮는 것이 일*이라 뺀다.
		//
		// ★ UnitTooltip 은 뺐다 — 그건 HUD 층이 아니라 **TooltipLayer** 에 붙는다(커서를 따라다니며
		//   무엇이든 덮어야 하는 물건이라 층이 다르다). HUD 층에서 찾으니 매번 「조각이 없음」이 떴고,
		//   그 상시 경고가 진짜 신호를 덮는다. 겹침 검사 대상도 아니다 — 덮는 것이 그 물건의 일이다.
		private static readonly string[] HUD_BLOCKS =
		{
			"ResourceBar", "ProgressPanel", "LegendPanel", "TowerDefenseSelectionBar",
			"HintBar", "RestartButton", "BoonSummary", "SelectionPanel", "Minimap",
		};

		/// <summary>
		/// 그 덩어리가 실제로 *차지한* 자리 — 전폭 띠는 껍데기가 화면을 가로지르지만 알맹이는 가운데
		/// 한 줌뿐이다. 껍데기로 재면 「가운데 띠가 좌측 범례를 가린다」 같은 거짓 겹침이 무더기로 나온다.
		/// 그래서 화면 폭을 거의 다 쓰는 껍데기는 *보이는 자식들이 실제로 덮은 범위*로 좁혀 잰다.
		/// </summary>
		private static Rect ContentBound(VisualElement block, float screenWidth)
		{
			Rect bound = block.worldBound;
			if (screenWidth <= 1f || bound.width < screenWidth * 0.95f)
				return bound;

			Rect union = Rect.zero;
			bool any = false;
			foreach (VisualElement child in block.Children())
			{
				if (child.resolvedStyle.display != DisplayStyle.Flex)
					continue;
				Rect childBound = ContentBound(child, screenWidth);
				if (childBound.width <= 1f || childBound.height <= 1f)
					continue;

				union = any ? Rect.MinMaxRect(
					Mathf.Min(union.xMin, childBound.xMin), Mathf.Min(union.yMin, childBound.yMin),
					Mathf.Max(union.xMax, childBound.xMax), Mathf.Max(union.yMax, childBound.yMax)) : childBound;
				any = true;
			}

			return any ? union : bound;
		}

		/// <summary>
		/// HUD 겹침 — 「미니맵이 선택 패널을 가리나」를 *사람 눈*이 아니라 좌표로 묻는다.
		///
		/// ★ 왜 스크린샷이 아니라 좌표인가: 그림은 사람이 봐야 하고, 봐야 하는 검사는 매번 미뤄진다
		///   (이 작업에서 「UI 겹침 확인」이 계속 뒤로 밀린 이유). 사각형이 겹치는지는 기계가 판정할 수 있다.
		/// ★ 화면 밖으로 나간 것도 잡는다 — 겹치지만 않으면 되는 게 아니라 *보여야* 한다.
		/// </summary>
		// 나가기 직전의 판 — 들어와서 이것과 같아야 「이어했다」다.
		// 시계가 이만큼은 쌓인 뒤에 나간다 — 0 에서 나가면 「되감김」과 「그대로」가 구분되지 않는다.
		private const int RESUME_MIN_CLOCK = 5;
		// 코어 레벨 한 단계는 넘기고도 남는 양 — 카드가 확실히 걸리게.
		private const int CORE_XP_FOR_CARDS = 500;
		// 건물 한 단계는 넘기고도 남는 양.
		private const int BUILDING_XP_FOR_PERKS = 300;
		private static int resumeSeed;
		private static int resumeResource;
		private static int resumeEssence;
		private static int resumeBuildings;
		private static int resumeSurvived;
		private static int resumeLives;
		private static int resumeTraps;
		private static int resumeWalls;

		private static void CaptureResumeSnapshot()
		{
			if (match == null)
				return;

			resumeSeed = match.MapSeed;
			resumeResource = match.Resource;
			resumeEssence = match.Essence;
			resumeBuildings = match.DollLabels.Count;
			resumeSurvived = match.SurvivedSeconds;
			resumeLives = match.Lives;
			resumeTraps = match.TrapCount;
			resumeWalls = match.WallCellCount;
			Debug.Log(TAG + " RESUME-SNAPSHOT seed=" + resumeSeed + " 자원=" + resumeResource
				+ " 정수=" + resumeEssence + " 건물=" + resumeBuildings
				+ " 버틴시간=" + resumeSurvived + " 목숨=" + resumeLives
				+ " 함정=" + resumeTraps + " 벽=" + resumeWalls);
		}

		/// <summary>
		/// 이어하기 — 나갔다 들어온 판이 나가기 전과 같은가.
		/// ★ 땅(씨앗)이 먼저다 — 건물 수만 맞고 땅이 다르면 내 건물이 엉뚱한 데 서 있는 것이다.
		/// ★ 지갑도 본다 — 되살리며 값을 또 치르면 이어할 때마다 지갑이 깎인다(실제 결함이었다).
		/// </summary>
		private static void VerifyResume()
		{
			TowerDefenseMatch resumed = Object.FindAnyObjectByType<TowerDefenseMatch>();
			// ★ 복원이 아직 도는 중이면 재지 않는다 — 중간값을 읽으면 멀쩡한 복원을 결함으로 잡는다.
			if (resumed != null && resumed.RestoreInProgress)
			{
				Debug.Log(TAG + " RESUME 대기 — 복원이 아직 도는 중이다(다음 틱에 다시 본다).");
				return;
			}
			if (resumed == null)
			{
				Debug.LogError(TAG + " RESUME-FAIL 재진입 후 매치가 없음");
				return;
			}

			string verdict = TAG + " RESUME seed " + resumeSeed + "→" + resumed.MapSeed
				+ " · 자원 " + resumeResource + "→" + resumed.Resource
				+ " · 정수 " + resumeEssence + "→" + resumed.Essence
				+ " · 건물 " + resumeBuildings + "→" + resumed.DollLabels.Count
				+ " · 버틴시간 " + resumeSurvived + "→" + resumed.SurvivedSeconds
				+ " · 목숨 " + resumeLives + "→" + resumed.Lives
				+ " · 함정 " + resumeTraps + "→" + resumed.TrapCount
				+ " · 벽 " + resumeWalls + "→" + resumed.WallCellCount;

			// ★ 시계가 안 돌아오면 오래 버틴 판이 이어하는 순간 처음으로 되감긴다(마수가 갑자기 약해진다).
			//   딱 맞을 필요는 없다 — 재진입에 걸린 몇 초는 흘러도 되지만, 0 으로 되감기면 안 된다.
			bool sameClock = resumed.SurvivedSeconds >= resumeSurvived;
			bool sameLives = resumed.Lives == resumeLives;
			bool sameGround = resumed.MapSeed == resumeSeed;
			bool sameWallet = resumed.Resource == resumeResource && resumed.Essence == resumeEssence;
			// ★ 「그 이상」이면 통과시키면 안 된다 — 유령이 한 채씩 느는 결함이 정확히 그렇게 숨어 있었다.
			bool sameBuildings = resumed.DollLabels.Count == resumeBuildings;
			// 함정·벽도 「그대로」의 일부다 — 인형만 세면 깔아둔 것이 사라져도 초록이 뜬다.
			bool sameField = resumed.TrapCount == resumeTraps && resumed.WallCellCount == resumeWalls;

			if (sameGround && sameWallet && sameBuildings && sameClock && sameLives && sameField)
				Debug.Log(verdict + " → 나갔다 들어와도 그 판 그대로 ✔");
			else
				Debug.LogError(verdict + " → 이어하기가 판을 그대로 못 돌려준다"
					+ (sameGround ? "" : " [땅이 다름]")
					+ (sameWallet ? "" : " [지갑이 다름]")
					+ (sameBuildings ? "" : " [건물 수가 다름]")
					+ (sameField ? "" : " [함정·벽이 다름]")
					+ (sameClock ? "" : " [시계가 되감김]")
					+ (sameLives ? "" : " [목숨이 다름]"));
		}

		/// <summary>
		/// 툴팁을 실제로 띄운다 — 마우스가 없는 하네스가 이걸 못 하면 툴팁 배치는 영영 미측정으로 남는다.
		/// ★ 손이 가장 자주 가는 자리(화면 오른쪽 아래 = 핫바 위)에서 띄운다. 가운데서만 재면
		///   「가장자리에서 화면 밖으로 새는가」라는 진짜 질문을 못 묻는다.
		/// </summary>
		private static void ShowTooltipForLayout()
		{
			if (TowerDefenseModeController.TryGetExistingInstance(out TowerDefenseModeController controller) == false)
				return;
			if (controller.Hud == null)
				return;

			controller.Hud.ShowUnitTooltip("확인용 설명 · 두 줄짜리",
				new Vector2(Screen.width * 0.9f, Screen.height * 0.15f));
		}

		// 남이 건넨 씨앗처럼 쓸 숫자 — 이 값으로 연 판이 정말 그 땅인지 본다.
		private const int SHARED_SEED = 20260803;
		private static bool seedShareArmed;

		/// <summary> 다음 판에 「남이 준 씨앗」을 걸어둔다. </summary>
		private static void ArmSeedShareCheck()
		{
			if (match == null)
				return;

			match.SetNextMatchSeed(SHARED_SEED);
			seedShareArmed = true;
		}

		/// <summary>
		/// 씨앗 공유 — 건넨 숫자로 연 판이 정말 그 땅인가.
		/// ★ 저장(이어하기)이 씨앗을 덮어쓸 수 있다 — 그러면 「공유」가 조용히 「이어하기」가 된다.
		///   그 경우는 실패가 아니라 *확인 못 함*이다(둘 다 씨앗을 정하는 정당한 주인이다).
		/// </summary>
		private static void VerifySeedShare()
		{
			if (seedShareArmed == false || match == null)
				return;

			seedShareArmed = false;
			string verdict = TAG + " SEED-SHARE 건넨씨앗=" + SHARED_SEED + " 열린판=" + match.MapSeed;

			if (match.MapSeed == SHARED_SEED)
				Debug.Log(verdict + " → 건넨 씨앗으로 같은 땅이 열린다 ✔");
			else
				Debug.Log(verdict + " → 이어하기가 씨앗을 정했다(저장이 우선) — 공유는 확인 못 함");
		}

		/// <summary>
		/// 코어를 실제로 골라 코어 카드를 띄운다 — 카드는 코어를 골라야 나온다.
		/// (수비대를 고르면 강화 선택지만 나오고 카드는 안 나온다 — 둘은 다른 화면이다.)
		/// </summary>
		private static void SelectCoreForLayout()
		{
			Transform stageRoot = FindStageRoot();
			if (TowerDefenseModeController.TryGetExistingInstance(out TowerDefenseModeController controller) == false)
				return;
			TowerDefensePlacement placement = controller.GetComponent<TowerDefensePlacement>();
			Camera modeCamera = ViewCameraResolver.Current;
			if (placement == null || modeCamera == null || match == null || stageRoot == null || match.CoreCombatant == null)
				return;

			placement.Disarm();
			placement.PlaceSelectedAt(WorldToScreen(modeCamera, match.CoreCombatant.Position));
		}

		/// <summary>
		/// 코어 카드 — 뜨는가, 그리고 *고르면 실제로 걸리는가*.
		/// ★ 화면에 떴다는 것만으론 부족하다 — 눌러도 아무 일도 안 일어나는 카드가 이 작업에서 이미 나왔다.
		/// </summary>
		private static void VerifyCoreCards()
		{
			if (match == null)
				return;

			VerifyHudLayout("코어 선택 중", mustBeUp: "SelectionPanel");

			List<TowerDefenseBoon> offers = new();
			match.OfferCoreCards(offers);
			int beforeTaken = match.BoonCount;
			bool chosen = offers.Count > 0 && match.ChooseCoreCard(0);

			string verdict = TAG + " CORE-CARDS pending=" + match.CorePendingChoices
				+ " offered=" + offers.Count + " chosen=" + chosen
				+ " 고른장수 " + beforeTaken + "→" + match.BoonCount
				+ " [" + match.BoonSummary + "]";

			if (offers.Count > 0 && chosen && match.BoonCount > beforeTaken)
				Debug.Log(verdict + " → 코어 카드가 뜨고 고르면 걸린다 ✔");
			else
				Debug.LogError(verdict + " → 카드가 안 뜨거나 골라도 안 걸린다.");
		}

		/// <summary>
		/// 건물 강화 선택지 — 자란 건물에 실제로 걸리는가.
		/// ★ 「선택지가 화면에 있다」와 「골라서 수치가 바뀐다」는 다른 얘기다. 뒤쪽까지 본다.
		/// </summary>
		private static void VerifyBuildingPerk()
		{
			if (match == null)
				return;

			MatchCombatant target = null;
			foreach (ICombatant combatant in match.RegisteredCombatants)
			{
				if (combatant is MatchCombatant matchCombatant == false)
					continue;
				if (matchCombatant.TeamId != 0 || matchCombatant.IsAlive == false)
					continue;
				if (match.CoreCombatant != null && matchCombatant == match.CoreCombatant)
					continue;

				target = matchCombatant;
				break;
			}

			if (target == null || match.GrantBuildingExperienceForVerification(target, BUILDING_XP_FOR_PERKS) == false)
			{
				Debug.Log(TAG + " PERK-SKIP 자라게 할 건물이 없음 — 확인 못 함");
				return;
			}

			TowerDefenseDollLabel doll = match.FindDoll(target);
			List<TowerDefenseBuildingPerk> offers = new();
			TowerDefenseBuildingProgress.Offer(doll.BuildingId, doll.Progress.Level, doll.IsHarvester, offers);

			int beforeTaken = doll.Progress.Taken.Count;
			int beforePending = doll.Progress.PendingChoices;
			bool applied = offers.Count > 0 && match.ChooseBuildingPerk(target, offers[0]);

			string verdict = TAG + " PERK level=" + doll.Progress.Level + " pending=" + beforePending
				+ " offered=" + offers.Count + " applied=" + applied
				+ " 고른수 " + beforeTaken + "→" + doll.Progress.Taken.Count;

			if (applied && doll.Progress.Taken.Count > beforeTaken)
				Debug.Log(verdict + " → 자란 건물이 선택지를 받고 고르면 걸린다 ✔");
			else
				Debug.LogError(verdict + " → 선택지가 안 나오거나 골라도 안 걸린다.");
		}

		/// <summary> 세워둔 건물을 실제로 골라 선택 패널을 띄운다(무장 해제 상태의 클릭 = 고르는 클릭). </summary>
		private static void SelectPlacedBuildingForLayout()
		{
			Transform stageRoot = FindStageRoot();
			if (TowerDefenseModeController.TryGetExistingInstance(out TowerDefenseModeController controller) == false)
				return;
			TowerDefensePlacement placement = controller.GetComponent<TowerDefensePlacement>();
			Camera modeCamera = ViewCameraResolver.Current;
			if (placement == null || modeCamera == null || match == null || stageRoot == null)
				return;

			// ★ *살아 있는* 건물을 고른다 — 앞서 판매 확인이 시험용 포탑을 팔아버려서, 그 자리를 누르면
			//   빈 땅을 누르는 꼴이 된다(그래서 패널이 영영 안 열렸다). 코어는 선택 대상이 아니다.
			MatchCombatant target = null;
			foreach (ICombatant combatant in match.RegisteredCombatants)
			{
				if (combatant is MatchCombatant matchCombatant == false)
					continue;
				if (matchCombatant.TeamId != 0 || matchCombatant.IsAlive == false)
					continue;
				if (match.CoreCombatant != null && matchCombatant == match.CoreCombatant)
					continue;

				target = matchCombatant;
				break;
			}

			if (target == null)
			{
				Debug.Log(TAG + " HUD-SELECT 고를 건물이 없음 — 선택 패널 배치는 확인 못 함");
				return;
			}

			placement.Disarm();
			placement.PlaceSelectedAt(WorldToScreen(modeCamera, target.Position));
		}

		/// <param name="mustBeUp">
		/// 이 상태에서 *반드시 떠 있어야* 하는 조각. 안 떠 있으면 실패다.
		/// ★ 없으면 「안 뜬 것은 겹칠 수도 없어서 겹침 0 이 「띄워본 적이 없다」를 숨긴다」가 그대로 일어난다 —
		///   실측에서 평상시·건물 선택 중·툴팁·코어 선택 중이 **네 상태 모두 똑같은 5개**를 재고 있었다.
		///   네 번 다 초록불이었지만 잰 것은 한 번뿐이었던 셈이다.
		/// </param>
		private static void VerifyHudLayout(string phase, string mustBeUp = null)
		{
			UIRoot uiRoot = Object.FindAnyObjectByType<UIRoot>();
			VisualElement hud = uiRoot != null && uiRoot.ModeHudLayer != null
				? uiRoot.ModeHudLayer.Q(nameof(TowerDefenseHudView))
				: null;
			if (hud == null)
			{
				Debug.LogError(TAG + " HUD-LAYOUT[" + phase + "] HUD 를 못 찾음");
				return;
			}

			if (mustBeUp != null && hud.Q(mustBeUp) == null)
			{
				Debug.LogError(TAG + " HUD-LAYOUT[" + phase + "]-FAIL 「" + mustBeUp
					+ "」가 안 떠 있다 — 띄운 줄 알고 잰 것이라 이 판정은 무의미하다.");
			}

			List<string> names = new();
			List<Rect> rects = new();
			foreach (string blockName in HUD_BLOCKS)
			{
				VisualElement block = hud.Q(blockName);
				if (block == null)
				{
					Debug.LogError(TAG + " HUD-LAYOUT[" + phase + "] 조각이 없음: " + blockName + " — 이름이 바뀌었거나 안 붙었다.");
					continue;
				}
				if (block.resolvedStyle.display != DisplayStyle.Flex)
					continue; // 지금 안 보이는 것은 겹칠 수도 없다.

				Rect bound = ContentBound(block, hud.worldBound.width);
				if (bound.width <= 1f || bound.height <= 1f)
					continue; // 아직 배치 전(폭 0) — 겹침 판정 대상이 아니다.

				names.Add(blockName);
				rects.Add(bound);
			}

			Rect screen = hud.worldBound;
			int overlaps = 0;
			for (int left = 0; left < rects.Count; left++)
			{
				if (screen.width > 1f && screen.Contains(new Vector2(rects[left].xMin + 1f, rects[left].yMin + 1f)) == false)
					Debug.LogError(TAG + " HUD-OFFSCREEN[" + phase + "] " + names[left] + " " + rects[left] + " 가 화면(" + screen + ") 밖으로 나감");

				for (int right = left + 1; right < rects.Count; right++)
				{
					if (rects[left].Overlaps(rects[right]) == false)
						continue;
					overlaps++;
					Debug.LogError(TAG + " HUD-OVERLAP[" + phase + "] " + names[left] + rects[left]
						+ " ↔ " + names[right] + rects[right]);
				}
			}

			string verdict = TAG + " HUD-LAYOUT[" + phase + "] blocks=" + names.Count + " overlaps=" + overlaps
				+ " [" + string.Join(",", names) + "]";
			if (overlaps == 0)
				Debug.Log(verdict + " → 겹치는 덩어리 없음 ✔");
		}

		/// <summary> 결말 화면 검증 — 배너가 실제로 떠야 플레이어가 끝났다는 걸 안다. </summary>
		private static void VerifyConclusion(double now)
		{
			if (now - restartAt < 1.0)
				return;

			UIRoot uiRoot = Object.FindAnyObjectByType<UIRoot>();
			VisualElement hud = uiRoot != null && uiRoot.ModeHudLayer != null
				? uiRoot.ModeHudLayer.Q(nameof(TowerDefenseHudView))
				: null;
			VisualElement banner = hud != null ? hud.Q("BannerWrapper") : null;

			bool bannerVisible = banner != null && banner.resolvedStyle.display == DisplayStyle.Flex;
			string bannerText = banner != null ? (banner.Q<Label>() != null ? banner.Q<Label>().text : "no-label") : "no-banner";

			if (bannerVisible)
				Debug.Log(TAG + " CONCLUSION-BANNER visible=True text=\"" + bannerText + "\"");
			else
				Debug.LogError(TAG + " CONCLUSION-BANNER 결과 배너가 안 뜸 — 끝났는데 화면이 아무 말도 안 한다. banner=" + (banner != null));

			// 결말 상태에서 「다시 도전」이 실제로 새 판을 여는가 (막다른 화면이 되지 않는가).
			if (TowerDefenseModeController.TryGetExistingInstance(out TowerDefenseModeController controller) == false)
			{
				Debug.LogError(TAG + " CONCLUSION-FAIL controller 없음");
				Finish();
				return;
			}

			Debug.Log(TAG + " CONCLUSION-RESTART 결말 상태에서 재시작 요청");
			controller.Restart();
			restartAt = now;
			step = Step.RestartFromConclusion;
		}

		/// <summary> 결말 → 재시작이 진짜 새 판인지 (자원/웨이브/국면이 처음으로 돌아왔는지). </summary>
		private static void VerifyRestartFromConclusion(double now)
		{
			if (now - restartAt < 3.0)
				return;

			if (match == null)
			{
				Debug.LogError(TAG + " CONCLUSION-RESTART-FAIL 매치 없음");
				Finish();
				return;
			}

			bool freshWave = match.WaveIndex == 0;
			bool freshOutcome = match.Outcome == TowerDefenseOutcome.InProgress;
			bool freshResource = match.Resource > 0;

			string verdict = TAG + " CONCLUSION-RESTART-RESULT wave=" + match.WaveIndex
				+ " outcome=" + match.Outcome
				+ " resource=" + match.Resource
				+ " phase=" + match.Phase;

					// ★ 다시 시작한 판은 신호가 **0 부터** 차야 한다. 안 비우면 두 번째 판이 이미 가득 찬 채로
					//   시작해 「점점 채워진다」가 통째로 사라진다(사용자가 콕 집어 요구한 것).
					TowerDefenseMatch fresh = Object.FindAnyObjectByType<TowerDefenseMatch>();
					if (fresh != null)
					{
						Debug.Log($"{TAG} 재시작 신호 — 코어 충전 {fresh.CoreSignalCharge:F2} (0 에 가까워야 한다)");
						if (fresh.CoreSignalCharge > 0.9f)
							Debug.LogError(TAG + " 재시작 FAIL — 새 판이 이미 가득 찬 신호로 시작한다(옛 판 상태가 남았다).");

						// ★ 새 판이 시작하자마자 「내 것이 부서졌다」가 뜨면, 판이 끝나며 청산된 것을
						//   적이 부순 것으로 오인한 것이다 — 첫인상이 거짓 경고면 알림 전체를 못 믿게 된다.
						int falseBreak = 0;
						foreach (TowerDefenseAlerts.Alert alert in fresh.Alerts)
						{
							if (alert.Label.Contains("부서졌다"))
								falseBreak++;
						}
						Debug.Log($"{TAG} 재시작 알림 — 「부서졌다」 {falseBreak}개 · 전체 {fresh.Alerts.Count}개 (둘 다 0 이어야 한다)");
						if (falseBreak > 0)
							Debug.LogError(TAG + " 재시작 FAIL — 새 판이 시작하자마자 옛 판 건물을 「부서졌다」고 알린다.");

						// ★ 그림도 판마다 하나여야 한다. 옛 판 것이 안 치워지면 신호장이 두 벌 겹쳐 그려지고,
						//   판을 거듭할수록 는다(눈에는 「좀 진해졌네」로만 보여서 늦게 발견된다).
						int fields = 0;
						foreach (GameObject candidate in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
						{
							if (candidate.name == "SignalField")
								fields++;
						}
						Debug.Log($"{TAG} 재시작 그림 — 신호장 {fields}벌 (1 이어야 한다)");
						if (fields > 1)
							Debug.LogError($"{TAG} 재시작 FAIL — 신호장이 {fields}벌 겹쳐 있다(옛 판 것이 안 치워졌다).");
					}

			if (freshWave && freshOutcome && freshResource)
				Debug.Log(verdict + " → 새 판 성립 ✔");
			else
				Debug.LogError(verdict + " → 결말 뒤 재시작이 새 판이 아니다(막다른 상태).");

			Finish();
		}

		private static void OnMatchEnded(TowerDefenseOutcome outcome)
		{
			matchEndedSeen = true;
			Debug.Log(TAG + " MATCH-ENDED outcome=" + outcome);
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
				if ((combatant.Position - stageRoot.position).sqrMagnitude < 100f * 100f)
					count++;
			}
			return count;
		}

		private static bool SceneIsWorld()
		{
			Scene active = SceneManager.GetActiveScene();
			return active.IsValid() && active.name == "World" && active.isLoaded;
		}

		private static void Finish()
		{
			// ★ 끝났다는 말을 안 하면 로그만 보고는 「끝났나 멈췄나」를 못 가린다 — 실제로 전체 실행이
			//   정상 종료했는데 10분을 「행」으로 의심하며 기다렸다. 검사는 자기 상태를 말해야 한다.
			double elapsed = EditorApplication.timeSinceStartup - playStart;
			Debug.Log($"{TAG} 검증 끝 — 마지막 단계 {step} · {elapsed:F0}초 · 모드 "
				+ (placeOnly ? "배치만" : conclusionOnly ? "결말만" : "전체")
				+ " (실패 항목은 위에 FAIL 로 찍힌다. 없으면 없다.)");

			EditorApplication.update -= Tick;
			if (match != null)
				match.MatchEnded -= OnMatchEnded;
			if (EditorApplication.isPlaying)
				EditorApplication.ExitPlaymode();
		}
	}
}
