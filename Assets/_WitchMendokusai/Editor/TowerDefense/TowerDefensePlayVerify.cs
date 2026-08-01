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
		private const string TAG = "[TD-Verify]";
		private const double SETTLE_SECONDS = 2.0;
		private const double HARD_TIMEOUT = 240.0;
		private const double OBSERVE_SECONDS = 70.0;
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
			Observe = 5,
		}

		private static Step step;
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

		static TowerDefensePlayVerify()
		{
			EditorApplication.playModeStateChanged += OnPlayModeChanged;
		}

		[MenuItem("WM/TowerDefense/Arm Play-Verify")]
		public static void Arm()
		{
			EditorPrefs.SetBool(ARM_PREF, true);
			Debug.Log(TAG + " armed — Play 진입");
			EditorApplication.EnterPlaymode();
		}

		private static void OnPlayModeChanged(PlayModeStateChange change)
		{
			if (change != PlayModeStateChange.EnteredPlayMode || EditorPrefs.GetBool(ARM_PREF, false) == false)
				return;

			EditorPrefs.SetBool(ARM_PREF, false);
			step = Step.WaitWorld;
			playStart = EditorApplication.timeSinceStartup;
			readyAt = -1.0;
			observeStart = -1.0;
			lastSample = -1.0;
			lastGateLog = -1.0;
			startClicked = false;
			match = null;
			matchEndedSeen = false;
			lastWaveIndex = -1;
			lastPhase = TowerDefensePhase.Concluded;
			lastResource = -1;
			firstContactWave = -1;
			EditorApplication.update += Tick;
			Debug.Log(TAG + " EnteredPlayMode — World ready 대기");
		}

		private static void Tick()
		{
			double now = EditorApplication.timeSinceStartup;

			// 안전망 — 무슨 일이 있어도 공유 에디터를 Play 에 물리지 않는다.
			if (now - playStart > HARD_TIMEOUT)
			{
				Debug.LogError(TAG + " TIMEOUT — 단계=" + step + " 에서 행. Play 강제 종료.");
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
					Debug.Log(TAG + " MATCH-READY resource=" + match.Resource + " phase=" + match.Phase);
					step = Step.Place;
					return;

				case Step.Place:
					DoPlacements();
					// 2차 덤프 — "진입 시엔 켜졌는데 이후 덮인다"를 잡으려면 시간 경과 후 한 번 더 봐야 한다.
					DumpCameras("배치 후");
					observeStart = now;
					lastSample = now;
					step = Step.Observe;
					return;

				case Step.Observe:
					Observe(now);
					return;
			}
		}

		/// <summary>
		/// 배치 검증 — **마우스 경로 그대로**: 목표 월드좌표를 모드 카메라로 화면좌표 환산 후
		/// TowerDefensePlacement.PlaceXAt(화면좌표) 호출. 즉 카메라 설정·지면 레이캐스트·셀 스냅·
		/// 노드 반경 판정까지 실제 클릭과 동일 경로를 탄다(버그가 숨는 자리가 바로 여기).
		/// 유일한 미포함 = 물리 마우스 버튼 이벤트 → InputStrategy 콜백 디스패치(얇은 글루).
		/// </summary>
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
			Transform camTransform = controller.transform.Find("StageRoot/ModeCamera");
			Camera modeCamera = camTransform != null ? camTransform.GetComponent<Camera>() : null;
			if (placement == null || modeCamera == null)
			{
				Debug.LogError(TAG + " PLACE-FAIL placement=" + (placement != null) + " modeCamera=" + (modeCamera != null));
				return;
			}
			Debug.Log(TAG + " PLACE-VIA-SCREEN camActive=" + modeCamera.gameObject.activeInHierarchy);

			int before = match.Resource;

			// 방어인형 먼저(개막 우선순위) — 코어와 적 스폰 사이 길목.
			Vector3[] towerLocals = { new Vector3(-3f, 0f, 6f), new Vector3(3f, 0f, 6f) };
			foreach (Vector3 local in towerLocals)
				placement.PlaceTowerAt(WorldToScreen(modeCamera, stageRoot.TransformPoint(local)));

			// 채집인형 = 자원 노드 위(반경 밖이면 거절돼야 정상).
			Vector3[] nodeLocals = { new Vector3(-10f, 0f, 10f), new Vector3(10f, 0f, 10f) };
			foreach (Vector3 local in nodeLocals)
				placement.PlaceHarvesterAt(WorldToScreen(modeCamera, stageRoot.TransformPoint(local)));

			// 노드에서 먼 빈 땅에 채집 시도 = 거절돼야 정상(노드 결합 규칙 살아있음 확인).
			int beforeOffNode = match.Resource;
			placement.PlaceHarvesterAt(WorldToScreen(modeCamera, stageRoot.TransformPoint(new Vector3(0f, 0f, 2f))));
			bool offNodeRejected = match.Resource == beforeOffNode;

			Debug.Log(TAG + " PLACE resourceBefore=" + before + " after=" + match.Resource
				+ " offNodeHarvesterRejected=" + offNodeRejected);

			LogHudState();
			LogNodeMarkers(stageRoot);
		}

		// HUD 실재 확인 — 화면에 숫자가 안 뜨면 사람이 플레이 판단을 못 한다(이번 증분의 핵심 산출).
		private static void LogHudState()
		{
			UIRoot uiRoot = Object.FindAnyObjectByType<UIRoot>();
			if (uiRoot == null || uiRoot.HudLayer == null)
			{
				Debug.LogError(TAG + " HUD-FAIL UIRoot/HudLayer 없음");
				return;
			}

			VisualElement hud = uiRoot.HudLayer.Q(nameof(TowerDefenseHudView));
			if (hud == null)
			{
				Debug.LogError(TAG + " HUD-FAIL HudLayer 에 TowerDefenseHudView 없음");
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

			Camera modeCamera = null;
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
				if (camera.name == "ModeCamera")
					modeCamera = camera;
				if (topmost == null || camera.depth > topmost.depth)
					topmost = camera;
			}

			// ★ 핵심 assert — "카메라 GameObject 가 active" 는 *화면에 보인다*가 아니다.
			//   플레이어 카메라 리그가 depth 100 이라 개척 카메라가 20 이면 켜져도 그 위에 덮여
			//   화면이 전혀 안 바뀐다(사용자 실증: "개척 UI는 뜨지만 플레이 불가"). 최상위 여부를 직접 판정.
			if (modeCamera == null)
			{
				Debug.LogError(TAG + " CAM-TOP[" + phase + "] 개척 카메라가 활성 목록에 없음");
				return;
			}

			bool isTopmost = topmost == modeCamera;
			string verdict = TAG + " CAM-TOP[" + phase + "] modeCameraDepth=" + modeCamera.depth
				+ " topmost=" + (topmost != null ? topmost.name + "(" + topmost.depth + ")" : "none")
				+ " 개척카메라가최상위=" + isTopmost;
			if (isTopmost)
				Debug.Log(verdict);
			else
				Debug.LogError(verdict + " → 화면이 안 바뀜(플레이 불가). 개척 카메라 depth 를 올려야 함.");
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

			if (matchEndedSeen || now - observeStart > OBSERVE_SECONDS)
			{
				Debug.Log(TAG + " SUMMARY endedEvent=" + matchEndedSeen
					+ " outcome=" + match.Outcome
					+ " wavesCleared=" + match.WaveIndex
					+ " resource=" + match.Resource
					+ " firstWaveSpawned=" + (firstContactWave >= 0)
					+ " observed=" + (now - observeStart).ToString("F1") + "s");
				Finish();
			}
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
