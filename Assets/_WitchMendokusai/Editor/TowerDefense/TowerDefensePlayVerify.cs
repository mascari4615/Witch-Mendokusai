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
					VerifySell();
					VerifyTrap();
					VerifyWall();
					VerifyResearch();
					VerifyWaveEvents();
					VerifySupply();
					// 씨앗 공유는 새 판에서만 확인된다 — 재시작이 그 새 판이므로 여기서 걸어둔다.
					ArmSeedShareCheck();
					step = placeOnly ? Step.SelectedLayout : Step.Restart;
					selectedLayoutAt = now;
					return;

				// ★ 패널은 *다음 배치 패스*에 열린다 — 클릭한 그 틱에 재면 「아직 안 뜬 것」을 재고
				//   「겹침 0」이라 적는다(거짓 통과). 한 틱 기다렸다 잰다.
				case Step.SelectedLayout:
					if (now - selectedLayoutAt < 0.3)
						return;
					// ★ 겹침은 한 번만 잰다 — 아래 시계 게이트가 이 단계를 여러 틱 돌리므로,
					//   안 막으면 같은 판정이 로그를 도배해 진짜 신호가 묻힌다.
					if (selectedLayoutChecked == false)
					{
						selectedLayoutChecked = true;
						VerifyHudLayout("건물 선택 중");
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
					VerifyResume();
					Debug.Log(TAG + " PLACE-ONLY 배치 확인 끝 — 조기 종료");
					Finish();
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
				case Step.DisarmRestart:
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
			IReadOnlyList<Vector3> nodeLocals = match.ActiveResourceNodePositions;
			if (nodeLocals.Count == 0)
				Debug.LogError(TAG + " PLACE-FAIL 스테이지에 자원 노드가 없음");
			// ★ 노드 전부에 세우면(6곳 × 60) 예산이 통째로 사라져 뒤의 확인이 전부 「돈이 없어 못 함」이 된다.
			//   여기서 볼 것은 「노드 위에 서는가」이므로 한 기면 충분하다.
			// ★ 채집 스폰은 코루틴(1프레임 양보 후 수입 반영)이라 *세운 그 틱에 읽으면 0*이다.
			//   그래서 확인(VerifyEssence)이 아니라 여기서 미리 세운다 — 1.5초 뒤에 읽힌다.
			//   바깥 노드(배수 큰 곳)를 우선 — 정수는 거기서만 난다.
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
		private static void LogHudState()
		{
			UIRoot uiRoot = Object.FindAnyObjectByType<UIRoot>();
			// 개척 HUD 는 OverlayLayer 에 붙는다 — 본편 HUD(HudLayer)를 통째 숨겨도 살아남아야 하기 때문.
			// HudLayer 를 보던 예전 assert 는 그 설계 변경 이후로 항상 실패하는 죽은 검사였다.
			if (uiRoot == null || uiRoot.OverlayLayer == null)
			{
				Debug.LogError(TAG + " HUD-FAIL UIRoot/OverlayLayer 없음");
				return;
			}

			VisualElement hud = uiRoot.OverlayLayer.Q(nameof(TowerDefenseHudView));
			if (hud == null)
			{
				Debug.LogError(TAG + " HUD-FAIL OverlayLayer 에 TowerDefenseHudView 없음");
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
			NPCObject[] npcs = Object.FindObjectsByType<NPCObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
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
			Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
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
			MCamera[] allVcams = Object.FindObjectsByType<MCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
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
			ArenaCombatant core = match.CoreCombatant;
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
				ArenaCombatant enemy = match.WaveEnemies[index];
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

				ArenaCombatant arena = combatant as ArenaCombatant;
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
				int ctrlCount = Object.FindObjectsByType<TowerDefenseModeController>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
				int matchCount = Object.FindObjectsByType<TowerDefenseMatch>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
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
				Debug.LogError(TAG + " DEFENDED-FAIL match null");
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
			foreach (TowerDefenseWeapon weapon in Object.FindObjectsByType<TowerDefenseWeapon>(FindObjectsInactive.Include, FindObjectsSortMode.None))
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
		private static void VerifyEssence()
		{
			Transform stageRoot = FindStageRoot();
			if (match == null || stageRoot == null)
				return;

			// 배치는 이미 DoPlacements 가 했다(코루틴이 끝날 시간을 벌기 위해) — 여기선 결과만 읽는다.
			string verdict = TAG + " ESSENCE harvesters=" + match.HarvesterCount
				+ " outer=" + match.OuterHarvesters
				+ " outerSupplied=" + match.SuppliedOuterHarvesters
				+ " outerPowered=" + match.PoweredOuterHarvesters
				+ " nextIncome=" + match.NextWaveIncome
				+ " nextEssence=" + match.NextWaveEssence
				+ " essence=" + match.Essence;

			// ★ 세 원인을 갈라 말한다 — 안 갈라 말하면 「바깥 노드인데 안 나온다」는 *거짓 실패*가 찍힌다
			//   (실측: 실제로는 바깥에 세운 적이 없거나, 세웠어도 사슬이 안 닿아 있었다).
			if (match.HarvesterCount == 0)
				Debug.Log(verdict + " → 채집을 못 세움(자원 부족/자리 없음) — 확인 못 함");
			else if (match.OuterHarvesters == 0)
				Debug.Log(verdict + " → 바깥 노드에 세운 게 없음(안쪽만 잡음) — 정수 0 은 정상, 확인 못 함");
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
		private static void VerifyResearch()
		{
			if (match == null)
				return;

			int cost = match.ResearchCost;
			if (match.Essence < cost)
			{
				Debug.Log(TAG + " RESEARCH-SKIP 정수 부족(" + match.Essence + "/" + cost + ") — 이번 실행에선 확인 못 함");
				return;
			}

			float multiplierBefore = match.TowerDamageMultiplier;
			int levelBefore = match.ResearchLevel;
			bool accepted = match.TryResearch();

			Debug.Log(TAG + " RESEARCH accepted=" + accepted
				+ " level " + levelBefore + " → " + match.ResearchLevel
				+ " damageMultiplier " + multiplierBefore.ToString("F2") + " → " + match.TowerDamageMultiplier.ToString("F2"));
			if (accepted && match.ResearchLevel == levelBefore)
				Debug.LogError(TAG + " RESEARCH-FAIL 정수를 받아놓고 단계가 안 올랐다.");
			if (accepted && match.TowerDamageMultiplier <= multiplierBefore)
				Debug.LogError(TAG + " RESEARCH-FAIL 단계는 올랐는데 포탑이 안 세졌다 — 연구가 하는 일이 없다.");
		}

		/// <summary> 승급 — 같은 자리에 같은 종류를 다시 지으면 단계가 오르고 사거리·피해가 자라는가. </summary>
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
			foreach (TowerDefenseWeapon weapon in Object.FindObjectsByType<TowerDefenseWeapon>(FindObjectsInactive.Include, FindObjectsSortMode.None))
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
			VisualElement hud = uiRoot != null && uiRoot.OverlayLayer != null
				? uiRoot.OverlayLayer.Q(nameof(TowerDefenseHudView))
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
		private static readonly string[] HUD_BLOCKS =
		{
			"ResourceBar", "ProgressPanel", "LegendPanel", "TowerDefenseSelectionBar",
			"HintBar", "RestartButton", "BoonSummary", "UnitTooltip", "SelectionPanel", "Minimap",
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
			Debug.Log(TAG + " RESUME-SNAPSHOT seed=" + resumeSeed + " 자원=" + resumeResource
				+ " 정수=" + resumeEssence + " 건물=" + resumeBuildings
				+ " 버틴시간=" + resumeSurvived + " 목숨=" + resumeLives);
		}

		/// <summary>
		/// 이어하기 — 나갔다 들어온 판이 나가기 전과 같은가.
		/// ★ 땅(씨앗)이 먼저다 — 건물 수만 맞고 땅이 다르면 내 건물이 엉뚱한 데 서 있는 것이다.
		/// ★ 지갑도 본다 — 되살리며 값을 또 치르면 이어할 때마다 지갑이 깎인다(실제 결함이었다).
		/// </summary>
		private static void VerifyResume()
		{
			TowerDefenseMatch resumed = Object.FindAnyObjectByType<TowerDefenseMatch>();
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
				+ " · 목숨 " + resumeLives + "→" + resumed.Lives;

			// ★ 시계가 안 돌아오면 오래 버틴 판이 이어하는 순간 처음으로 되감긴다(마수가 갑자기 약해진다).
			//   딱 맞을 필요는 없다 — 재진입에 걸린 몇 초는 흘러도 되지만, 0 으로 되감기면 안 된다.
			bool sameClock = resumed.SurvivedSeconds >= resumeSurvived;
			bool sameLives = resumed.Lives == resumeLives;
			bool sameGround = resumed.MapSeed == resumeSeed;
			bool sameWallet = resumed.Resource == resumeResource && resumed.Essence == resumeEssence;
			// ★ 「그 이상」이면 통과시키면 안 된다 — 유령이 한 채씩 느는 결함이 정확히 그렇게 숨어 있었다.
			bool sameBuildings = resumed.DollLabels.Count == resumeBuildings;

			if (sameGround && sameWallet && sameBuildings && sameClock && sameLives)
				Debug.Log(verdict + " → 나갔다 들어와도 그 판 그대로 ✔");
			else
				Debug.LogError(verdict + " → 이어하기가 판을 그대로 못 돌려준다"
					+ (sameGround ? "" : " [땅이 다름]")
					+ (sameWallet ? "" : " [지갑이 다름]")
					+ (sameBuildings ? "" : " [건물 수가 다름]")
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

			VerifyHudLayout("코어 선택 중");

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

			ArenaCombatant target = null;
			foreach (ICombatant combatant in match.RegisteredCombatants)
			{
				if (combatant is ArenaCombatant arenaCombatant == false)
					continue;
				if (arenaCombatant.TeamId != 0 || arenaCombatant.IsAlive == false)
					continue;
				if (match.CoreCombatant != null && arenaCombatant == match.CoreCombatant)
					continue;

				target = arenaCombatant;
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
			ArenaCombatant target = null;
			foreach (ICombatant combatant in match.RegisteredCombatants)
			{
				if (combatant is ArenaCombatant arenaCombatant == false)
					continue;
				if (arenaCombatant.TeamId != 0 || arenaCombatant.IsAlive == false)
					continue;
				if (match.CoreCombatant != null && arenaCombatant == match.CoreCombatant)
					continue;

				target = arenaCombatant;
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

		private static void VerifyHudLayout(string phase)
		{
			UIRoot uiRoot = Object.FindAnyObjectByType<UIRoot>();
			VisualElement hud = uiRoot != null && uiRoot.OverlayLayer != null
				? uiRoot.OverlayLayer.Q(nameof(TowerDefenseHudView))
				: null;
			if (hud == null)
			{
				Debug.LogError(TAG + " HUD-LAYOUT[" + phase + "] HUD 를 못 찾음");
				return;
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
			VisualElement hud = uiRoot != null && uiRoot.OverlayLayer != null
				? uiRoot.OverlayLayer.Q(nameof(TowerDefenseHudView))
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
			ArenaCombatant[] combatants = Object.FindObjectsByType<ArenaCombatant>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
			foreach (ArenaCombatant combatant in combatants)
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
			EditorApplication.update -= Tick;
			if (match != null)
				match.MatchEnded -= OnMatchEnded;
			if (EditorApplication.isPlaying)
				EditorApplication.ExitPlaymode();
		}
	}
}
